using MakroPad.Core.Protocol;

namespace MakroPad.Core.Tests.Protocol;

public sealed class ProtocolCodecTests
{
    [Fact]
    public void ParseReadsTypeAndValues()
    {
        var message = ProtocolCodec.Parse("button;id=1;state=up");

        Assert.Equal("button", message.Type);
        Assert.Equal("1", message.GetValue("id"));
        Assert.Equal("up", message.GetValue("state"));
    }

    [Fact]
    public void FormatPercentEncodesReservedCharacters()
    {
        var line = ProtocolCodec.Format(
            "display",
            new Dictionary<string, string?>
            {
                ["text"] = "Volume=50%;line\n2"
            });

        Assert.Equal("display;text=Volume%3D50%25%3Bline%0A2", line);
    }

    [Fact]
    public void ParsePercentDecodesReservedCharacters()
    {
        var message = ProtocolCodec.Parse("display;text=Volume%3D50%25%3Bline%0A2");

        Assert.Equal("Volume=50%;line\n2", message.GetValue("text"));
    }

    [Fact]
    public void RoundTripPreservesUnicode()
    {
        var line = ProtocolCodec.Format(
            "display",
            new Dictionary<string, string?>
            {
                ["artist"] = "Björk",
                ["title"] = "Jóga"
            });

        var message = ProtocolCodec.Parse(line);

        Assert.Equal("Björk", message.GetValue("artist"));
        Assert.Equal("Jóga", message.GetValue("title"));
    }

    [Fact]
    public void ParseThrowsForEmptyLine()
    {
        Assert.Throws<ArgumentException>(() => ProtocolCodec.Parse(""));
    }

    [Fact]
    public void ParseUsesLastDuplicateKey()
    {
        var message = ProtocolCodec.Parse("slider;value=1;value=2");

        Assert.Equal("2", message.GetValue("value"));
    }
}
