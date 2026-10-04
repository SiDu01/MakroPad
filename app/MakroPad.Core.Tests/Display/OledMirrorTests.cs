using MakroPad.Core.Display;
using Microsoft.Extensions.Time.Testing;

namespace MakroPad.Core.Tests.Display;

public sealed class OledMirrorTests
{
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));
    private readonly OledMirror mirror;

    public OledMirrorTests() => mirror = new OledMirror(time);

    [Fact]
    public void IdleScreenHasInvertedHeaderAndClock()
    {
        mirror.SetHostConnected(true);
        mirror.Apply("state;profile=Default;time=18:42:05;date=04.10.2026");

        var frame = mirror.Render();

        // Header bar is lit except where the black text is drawn.
        Assert.True(frame[0, 0]);
        Assert.True(frame[127, 11]);
        Assert.False(frame[0, 12]);
        Assert.Equal(Render("18:42", 3, 19, 20), Region(frame, 19, 20, 90, 24));
    }

    [Fact]
    public void ClockKeepsRunningWithoutNewState()
    {
        mirror.SetHostConnected(true);
        mirror.Apply("state;profile=Default;time=23:59:30;date=04.10.2026");
        var before = Region(mirror.Render(), 19, 20, 90, 24);

        time.Advance(TimeSpan.FromSeconds(45));

        Assert.NotEqual(before, Region(mirror.Render(), 19, 20, 90, 24));
        Assert.Equal(Render("00:00", 3, 19, 20), Region(mirror.Render(), 19, 20, 90, 24));
    }

    [Fact]
    public void OverlayExpiresBackToIdle()
    {
        mirror.SetHostConnected(true);
        mirror.Apply("state;profile=Default;time=12:00:00;date=04.10.2026");
        var idle = mirror.Render().Pixels.ToArray();

        mirror.Apply("screen;type=volume;text=System;value=50;ms=1000");
        var overlay = mirror.Render();
        Assert.True(overlay[1, 51]);    // progress bar fill starts
        Assert.True(overlay[63, 55]);   // 50 % filled
        Assert.False(overlay[100, 55]); // empty half

        time.Advance(TimeSpan.FromMilliseconds(1100));
        Assert.Equal(idle, mirror.Render().Pixels.ToArray());
    }

    [Fact]
    public void DisconnectShowsNoAppHeader()
    {
        mirror.SetHostConnected(false);
        var frame = mirror.Render();

        // "Keine App" in black on the header: the first letter leaves a gap at x=2.
        Assert.False(frame[2, 2]);
    }

    [Fact]
    public void UmlautsUseCodePage437()
    {
        Assert.Equal(new byte[] { 0x4C, 0x61, 0x75, 0x74, 0x73, 0x74, 0x84, 0x72, 0x6B, 0x65 }, DisplayText.ToCp437("Lautstärke"));
        Assert.Equal(new byte[] { (byte)'?' }, DisplayText.ToCp437("😀"));
    }

    [Fact]
    public void MarqueeMatchesFirmware()
    {
        var text = DisplayText.ToCp437("abcdefgh");
        Assert.Equal("gh  "u8.ToArray(), DisplayText.MarqueeWindow(text, 4, 6));
        Assert.Equal(11, DisplayText.MarqueeCycle(8, 4));
    }

    private static bool[] Render(string text, int size, int x, int y)
    {
        var canvas = new OledCanvas();
        canvas.Print(x, y, DisplayText.ToCp437(text), size, true);
        return Region(canvas, x, y, 90, 24);
    }

    private static bool[] Region(OledCanvas canvas, int x, int y, int width, int height)
    {
        var result = new bool[width * height];
        for (var row = 0; row < height; row++)
        {
            for (var column = 0; column < width; column++)
            {
                result[(row * width) + column] = canvas[x + column, y + row];
            }
        }

        return result;
    }
}
