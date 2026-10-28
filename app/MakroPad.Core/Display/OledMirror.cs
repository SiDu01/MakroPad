using System.Globalization;
using MakroPad.Core.Protocol;

namespace MakroPad.Core.Display;

/// <summary>
/// Software twin of the firmware's display module (firmware/src/display.cpp). It is fed the same
/// <c>state</c>/<c>screen</c>/<c>clear</c> lines the device receives and renders the same pixels,
/// so the app can show what the OLED shows right now. Keep both in sync when changing layouts.
/// </summary>
public sealed class OledMirror
{
    private const int CharWidth = 6;
    private const int HeaderHeight = 12;
    private const int DefaultOverlayMs = 1500;
    private const int ScrollStepMs = 220;
    private const int ScrollStartPauseMs = 900;

    private readonly TimeProvider timeProvider;
    private readonly object gate = new();
    private readonly OledCanvas canvas = new();

    private bool hostConnected;
    private bool displayOn = true;
    private string profile = "";
    private string artist = "";
    private string title = "";
    private DateTime? clockBase;
    private long clockBaseTimestamp;
    private Overlay? overlay;
    private bool scrolling;
    private int scrollStep;
    private long lastScrollTimestamp;

    public OledMirror(TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider;
        lastScrollTimestamp = timeProvider.GetTimestamp();
    }

    private sealed record Overlay(ScreenType Type, string Title, string Text, int Value, ScreenIcon Icon, long? UntilTimestamp);

    public void SetHostConnected(bool connected)
    {
        lock (gate)
        {
            hostConnected = connected;
            if (!connected)
            {
                overlay = null;
                displayOn = true;
            }

            ResetScroll();
        }
    }

    /// <summary>Feeds a line the app sent to the device. Other message types are ignored.</summary>
    public void Apply(string line)
    {
        ProtocolMessage message;
        try
        {
            message = ProtocolCodec.Parse(line);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            return;
        }

        lock (gate)
        {
            switch (message.Type)
            {
                case "state":
                    ApplyState(message);
                    break;
                case "screen":
                    ApplyScreen(message);
                    break;
                case "clear":
                    overlay = null;
                    ResetScroll();
                    break;
                case "display":
                    displayOn = message.GetValue("on") != "0";
                    break;
            }
        }
    }

    /// <summary>Renders the current frame (advancing timeouts and scrolling like the firmware loop).</summary>
    public OledCanvas Render()
    {
        lock (gate)
        {
            var now = timeProvider.GetTimestamp();
            if (overlay?.UntilTimestamp is { } until && now >= until)
            {
                overlay = null;
                ResetScroll();
            }

            if (scrolling)
            {
                var interval = scrollStep == 0 ? ScrollStartPauseMs : ScrollStepMs;
                if (timeProvider.GetElapsedTime(lastScrollTimestamp, now).TotalMilliseconds >= interval)
                {
                    scrollStep++;
                    lastScrollTimestamp = now;
                }
            }

            scrolling = false;
            canvas.Clear();
            if (!displayOn)
            {
                return canvas;
            }

            if (overlay is not null)
            {
                DrawOverlay(overlay);
            }
            else
            {
                DrawIdle();
            }

            return canvas;
        }
    }

    private void ApplyState(ProtocolMessage message)
    {
        profile = message.GetValue("profile") ?? profile;
        artist = message.GetValue("artist") ?? artist;
        title = message.GetValue("title") ?? title;
        if (message.GetValue("time") is { } time && message.GetValue("date") is { } date
            && DateTime.TryParseExact($"{date} {time}", ["dd.MM.yyyy HH:mm:ss", "dd.MM.yyyy HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            clockBase = parsed;
            clockBaseTimestamp = timeProvider.GetTimestamp();
        }

        if (overlay is null)
        {
            ResetScroll();
        }
    }

    private void ApplyScreen(ProtocolMessage message)
    {
        var type = (message.GetValue("type") ?? "").ToLowerInvariant() switch
        {
            "volume" => ScreenType.Volume,
            "action" => ScreenType.Action,
            "media" => ScreenType.Media,
            "profile" => ScreenType.Profile,
            _ => ScreenType.Status
        };
        var icon = (message.GetValue("icon") ?? "").ToLowerInvariant() switch
        {
            "ok" => ScreenIcon.Ok,
            "fail" => ScreenIcon.Fail,
            "warn" => ScreenIcon.Warn,
            "sync" => ScreenIcon.Sync,
            _ => ScreenIcon.None
        };
        var ms = int.TryParse(message.GetValue("ms"), out var parsedMs) && parsedMs >= 0 ? parsedMs : DefaultOverlayMs;
        long? until = ms == 0 ? null : timeProvider.GetTimestamp() + (long)(ms / 1000.0 * timeProvider.TimestampFrequency);
        overlay = new Overlay(
            type,
            message.GetValue("title") ?? "",
            message.GetValue("text") ?? "",
            int.TryParse(message.GetValue("value"), out var value) ? value : 0,
            icon,
            until);
        ResetScroll();
    }

    private void ResetScroll()
    {
        scrollStep = 0;
        lastScrollTimestamp = timeProvider.GetTimestamp();
    }

    private DateTime? Now => clockBase?.Add(timeProvider.GetElapsedTime(clockBaseTimestamp));

    private void DrawText(int x, int y, string text, int size, int maxChars, bool center = false, bool on = true)
    {
        var converted = DisplayText.ToCp437(text);
        var visible = Math.Max(1, maxChars);
        if (DisplayText.MarqueeCycle(converted.Length, visible) > 0)
        {
            scrolling = true;
        }

        var shown = DisplayText.MarqueeWindow(converted, visible, scrollStep);
        if (center)
        {
            var width = shown.Length * CharWidth * size;
            x = width < OledCanvas.Width ? (OledCanvas.Width - width) / 2 : 0;
        }

        canvas.Print(x, y, shown, size, on);
    }

    private void DrawHeader(string text)
    {
        canvas.FillRect(0, 0, OledCanvas.Width, HeaderHeight, true);
        DrawText(2, 2, text, 1, (OledCanvas.Width - 4) / CharWidth, on: false);
    }

    private void DrawHeader(string left, string right)
    {
        canvas.FillRect(0, 0, OledCanvas.Width, HeaderHeight, true);
        var rightX = OledCanvas.Width - 2 - (right.Length * CharWidth);
        DrawText(2, 2, left, 1, (rightX - 2 - CharWidth) / CharWidth, on: false);
        DrawText(rightX, 2, right, 1, right.Length, on: false);
    }

    private void DrawTrack(string trackTitle, string trackArtist)
    {
        DrawText(0, 19, trackTitle, 2, OledCanvas.Width / (CharWidth * 2), center: true);
        DrawText(0, 42, trackArtist, 1, OledCanvas.Width / CharWidth, center: true);
    }

    private void DrawIdle()
    {
        var header = !hostConnected ? "Keine App" : profile.Length > 0 ? profile : "MakroPad";
        var now = Now;
        var time = now is { } clock ? clock.ToString("HH:mm", CultureInfo.InvariantCulture) : "--:--";

        // While music plays the track takes the space and the clock moves into the header.
        if (title.Length > 0)
        {
            DrawHeader(header, time);
            DrawTrack(title, artist);
            return;
        }

        DrawHeader(header);
        DrawText(0, 20, time, 3, 7, center: true);

        var bottom = now is { } today ? today.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)
            : !hostConnected ? "Warte auf App" : "";
        DrawText(0, 52, bottom, 1, OledCanvas.Width / CharWidth, center: true);
    }

    private void DrawOverlay(Overlay current)
    {
        const int fullWidthChars = OledCanvas.Width / CharWidth;
        switch (current.Type)
        {
            case ScreenType.Volume:
                DrawHeader(current.Title.Length > 0 ? current.Title : "Lautstärke");
                DrawText(0, 16, current.Text, 1, fullWidthChars);
                DrawText(0, 28, $"{Math.Clamp(current.Value, 0, 100)}%", 2, 5);
                DrawProgressBar(0, 50, OledCanvas.Width, 12, current.Value);
                break;
            case ScreenType.Media:
                DrawHeader("Wiedergabe");
                DrawTrack(current.Title, current.Text);
                break;
            case ScreenType.Profile:
                DrawHeader(current.Title.Length > 0 ? current.Title : "Profil");
                DrawText(0, 30, current.Text, 2, OledCanvas.Width / (CharWidth * 2), center: true);
                break;
            default:
                DrawHeader(current.Title.Length > 0 ? current.Title : current.Type == ScreenType.Action ? "Aktion" : "Status");
                DrawIcon(current.Icon, 0, 24);
                var x = current.Icon == ScreenIcon.None ? 0 : 24;
                DrawText(x, 30, current.Text, 1, (OledCanvas.Width - x) / CharWidth);
                break;
        }
    }

    private void DrawProgressBar(int x, int y, int width, int height, int percent)
    {
        canvas.DrawRect(x, y, width, height, true);
        var fill = (width - 2) * Math.Clamp(percent, 0, 100) / 100;
        if (fill > 0)
        {
            canvas.FillRect(x + 1, y + 1, fill, height - 2, true);
        }
    }

    private void DrawIcon(ScreenIcon icon, int x, int y)
    {
        switch (icon)
        {
            case ScreenIcon.Ok:
                canvas.DrawCircle(x + 9, y + 9, 8, true);
                canvas.DrawLine(x + 5, y + 9, x + 8, y + 12, true);
                canvas.DrawLine(x + 8, y + 12, x + 14, y + 5, true);
                break;
            case ScreenIcon.Warn:
                canvas.DrawTriangle(x + 9, y + 1, x + 1, y + 17, x + 17, y + 17, true);
                canvas.DrawLine(x + 9, y + 6, x + 9, y + 11, true);
                canvas.SetPixel(x + 9, y + 14, true);
                break;
            case ScreenIcon.Fail:
                canvas.DrawRect(x + 1, y + 1, 17, 17, true);
                canvas.DrawLine(x + 5, y + 5, x + 13, y + 13, true);
                canvas.DrawLine(x + 13, y + 5, x + 5, y + 13, true);
                break;
            case ScreenIcon.Sync:
                canvas.DrawCircle(x + 9, y + 9, 7, true);
                canvas.DrawLine(x + 13, y + 3, x + 17, y + 3, true);
                canvas.DrawLine(x + 17, y + 3, x + 15, y + 1, true);
                canvas.DrawLine(x + 5, y + 15, x + 1, y + 15, true);
                canvas.DrawLine(x + 1, y + 15, x + 3, y + 17, true);
                break;
        }
    }
}
