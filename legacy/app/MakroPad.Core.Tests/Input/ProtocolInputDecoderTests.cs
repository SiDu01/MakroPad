using MakroPad.Core.Input;
using MakroPad.Core.Protocol;

namespace MakroPad.Core.Tests.Input;

public sealed class ProtocolInputDecoderTests
{
    private readonly ProtocolInputDecoder decoder = new();

    [Fact]
    public void DecodesButtonRelease()
    {
        var message = ProtocolCodec.Parse("button;id=3;state=up");

        Assert.True(decoder.TryDecode(message, out var inputEvent));
        var button = Assert.IsType<ButtonReleasedInputEvent>(inputEvent);
        Assert.Equal(3, button.ButtonId);
        Assert.Equal("button:3", button.Input);
    }

    [Theory]
    [InlineData("button;id=3;state=down")]
    [InlineData("button;id=3")]
    [InlineData("button;id=0;state=up")]
    [InlineData("button;state=up")]
    public void IgnoresInvalidOrNonReleaseButtonMessages(string line)
    {
        Assert.False(decoder.TryDecode(ProtocolCodec.Parse(line), out _));
    }

    [Fact]
    public void DecodesSliderChange()
    {
        var message = ProtocolCodec.Parse("slider;id=2;value=2048;raw=2048");

        Assert.True(decoder.TryDecode(message, out var inputEvent));
        var slider = Assert.IsType<SliderChangedInputEvent>(inputEvent);
        Assert.Equal(2, slider.SliderId);
        Assert.Equal(2048, slider.Value);
        Assert.Equal("slider:2", slider.Input);
    }

    [Theory]
    [InlineData("slider;id=2")]
    [InlineData("slider;id=0;value=100")]
    [InlineData("ready;uptime=100")]
    public void IgnoresUnsupportedMessages(string line)
    {
        Assert.False(decoder.TryDecode(ProtocolCodec.Parse(line), out _));
    }
}
