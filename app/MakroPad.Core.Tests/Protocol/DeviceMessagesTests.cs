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
}
