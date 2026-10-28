using System.Globalization;
using System.Text;

namespace MakroPad.Core.Protocol;

/// <summary>Protocol v2 constants and builders for app → device messages (docs/PROTOCOL.md).</summary>
public static class DeviceMessages
{
    public const int ProtocolVersion = 2;
    public const string DeviceName = "makropad";

    /// <summary>The firmware drops longer lines (bytes, without the line break).</summary>
    public const int MaxLineBytes = 256;

    public static string HelloAck(string appVersion) => ProtocolCodec.Format("hello-ack", new Dictionary<string, string?>
    {
        ["proto"] = ProtocolVersion.ToString(CultureInfo.InvariantCulture),
        ["app"] = appVersion
    });

    public static string Ping(int id) => ProtocolCodec.Format("ping", new Dictionary<string, string?>
    {
        ["id"] = id.ToString(CultureInfo.InvariantCulture)
    });

    public static string Pong(string? id) => ProtocolCodec.Format("pong", new Dictionary<string, string?>
    {
        ["id"] = id
    });

    public static string State(DeviceState state) => Fit("state", new Dictionary<string, string?>
    {
        ["profile"] = state.Profile,
        ["time"] = state.Now?.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
        ["date"] = state.Now?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
        ["artist"] = state.Artist,
        ["title"] = state.Title,
        ["volume"] = state.Volume?.ToString(CultureInfo.InvariantCulture)
    }, "artist", "title");

    public static string Screen(ScreenRequest screen) => Fit("screen", new Dictionary<string, string?>
    {
        ["type"] = screen.Type.ToString().ToLowerInvariant(),
        ["title"] = screen.Title,
        ["text"] = screen.Text,
        ["value"] = screen.Value?.ToString(CultureInfo.InvariantCulture),
        ["icon"] = screen.Icon == ScreenIcon.None ? null : screen.Icon.ToString().ToLowerInvariant(),
        ["ms"] = screen.DurationMs?.ToString(CultureInfo.InvariantCulture)
    }, "title", "text");

    public static string Clear() => "clear";

    /// <summary>Shortens the longest of <paramref name="shortenable"/> until the line fits the firmware's limit.</summary>
    private static string Fit(string type, Dictionary<string, string?> values, params string[] shortenable)
    {
        var line = ProtocolCodec.Format(type, values);
        while (Encoding.UTF8.GetByteCount(line) > MaxLineBytes
               && shortenable.MaxBy(key => values[key]?.Length ?? 0) is { } key
               && values[key] is { Length: > 0 } value)
        {
            // Never split a surrogate pair.
            values[key] = value[..^(value.Length > 1 && char.IsLowSurrogate(value[^1]) ? 2 : 1)];
            line = ProtocolCodec.Format(type, values);
        }

        return line;
    }
}

/// <summary>Idle-screen state. Null fields are not sent (device keeps its value); empty strings clear.</summary>
public sealed record DeviceState(
    string? Profile = null,
    DateTime? Now = null,
    string? Artist = null,
    string? Title = null,
    int? Volume = null);

public enum ScreenType
{
    Volume,
    Action,
    Media,
    Profile,
    Status
}

public enum ScreenIcon
{
    None,
    Ok,
    Fail,
    Warn,
    Sync
}

public sealed record ScreenRequest(
    ScreenType Type,
    string? Title = null,
    string? Text = null,
    int? Value = null,
    ScreenIcon Icon = ScreenIcon.None,
    int? DurationMs = null);
