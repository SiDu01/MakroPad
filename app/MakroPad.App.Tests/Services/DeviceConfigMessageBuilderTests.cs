using MakroPad.App.Services;
using MakroPad.Core.Protocol;

namespace MakroPad.App.Tests.Services;

public sealed class DeviceConfigMessageBuilderTests
{
    [Fact]
    public void BuildCreatesDeterministicConfigMessage()
    {
        var builder = new DeviceConfigMessageBuilder();
        var state = new DeviceDisplayState(
            "Gaming",
            "Connected",
            3000,
            3,
            "MakroPad",
            "{profile}",
            "{status}",
            "",
            "",
            true,
            new NowPlayingTrack("Artist", "Title"));

        var message = ProtocolCodec.Parse(builder.Build(state, new DateTime(2026, 6, 3, 14, 5, 0), 42));

        Assert.Equal("Gaming", message.GetValue("profile"));
        Assert.Equal("14:05", message.GetValue("time"));
        Assert.Equal("03.06.2026", message.GetValue("date"));
        Assert.Equal("42%", message.GetValue("volumeSystem"));
        Assert.Equal("media", message.GetValue("preferredDisplay"));
    }

    [Fact]
    public void MediaPlaceholderKeepsProfileDisplayPreferred()
    {
        var builder = new DeviceConfigMessageBuilder();
        var state = new DeviceDisplayState(
            "Default",
            "Connected",
            3000,
            3,
            "MakroPad",
            "{media.track}",
            "{status}",
            "",
            "",
            true,
            new NowPlayingTrack("Artist", "Title"));

        var message = ProtocolCodec.Parse(builder.Build(state, DateTime.Now, null));

        Assert.Equal("profile", message.GetValue("preferredDisplay"));
        Assert.Null(message.GetValue("volumeSystem"));
    }
}
