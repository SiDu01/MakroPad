using MakroPad.Core.Protocol;

namespace MakroPad.Core.Tests.Protocol;

public sealed class DeviceMessagesTests
{
    [Fact]
    public void StateSendsOnlyProvidedFields()
    {
        var line = DeviceMessages.State(new DeviceState(Profile: "Gaming", Now: new DateTime(2026, 10, 4, 18, 42, 5), Title: ""));

        Assert.Equal("state;profile=Gaming;time=18:42:05;date=04.10.2026;title=", line);
    }

    [Fact]
    public void ScreenUsesLowercaseTypeAndIcon()
    {
        var line = DeviceMessages.Screen(new ScreenRequest(ScreenType.Action, "Aktion", "Play/Pause", Icon: ScreenIcon.Ok, DurationMs: 0));

        Assert.Equal("screen;type=action;title=Aktion;text=Play/Pause;icon=ok;ms=0", line);
    }

    [Fact]
    public void ScreenEncodesReservedCharacters()
    {
        var line = DeviceMessages.Screen(new ScreenRequest(ScreenType.Volume, Text: "A;B", Value: 72));

        Assert.Equal("screen;type=volume;text=A%3BB;value=72", line);
    }

    [Fact]
    public void LongTrackIsShortenedToFitTheFirmwareLineLimit()
    {
        var title = "Symphonie Nr. 9 „Choral“: IV. Presto – Allegro assai – Andante maestoso – " + new string('ä', 100) + "😀";
        var line = DeviceMessages.State(new DeviceState(Profile: "Default", Now: new DateTime(2026, 10, 4, 18, 42, 5), Artist: "Wiener Philharmoniker", Title: title));

        Assert.InRange(System.Text.Encoding.UTF8.GetByteCount(line), 200, DeviceMessages.MaxLineBytes);
        var parsed = ProtocolCodec.Parse(line);
        Assert.Equal("Default", parsed.GetValue("profile"));
        Assert.Equal("18:42:05", parsed.GetValue("time"));
        Assert.Equal("Wiener Philharmoniker", parsed.GetValue("artist"));
        Assert.StartsWith(parsed.GetValue("title"), title, StringComparison.Ordinal);
    }
}
