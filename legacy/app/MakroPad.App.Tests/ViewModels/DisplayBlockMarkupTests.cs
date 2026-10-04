using MakroPad.App.ViewModels;

namespace MakroPad.App.Tests.ViewModels;

public sealed class DisplayBlockMarkupTests
{
    [Fact]
    public void ParseAndSerializeRoundTripsTemplate()
    {
        const string template = "# Title\nStatus {status}\n---\n- One";

        var blocks = DisplayBlockMarkup.Parse(template);

        Assert.Equal(4, blocks.Count);
        Assert.Equal(DisplayBlockKind.Heading, blocks[0].Kind);
        Assert.Equal(DisplayBlockKind.Text, blocks[1].Kind);
        Assert.Equal(DisplayBlockKind.Divider, blocks[2].Kind);
        Assert.Equal(DisplayBlockKind.ListItem, blocks[3].Kind);
        Assert.Equal(template, DisplayBlockMarkup.Serialize(blocks));
    }

    [Fact]
    public void ParseAndSerializeHandlesBlankLinesAndAlignment()
    {
        const string template = "[c]# Titel\n\n[r]Rechts";

        var blocks = DisplayBlockMarkup.Parse(template);

        Assert.Equal(3, blocks.Count);
        Assert.Equal(DisplayBlockKind.Heading, blocks[0].Kind);
        Assert.Equal("center", blocks[0].Alignment);
        Assert.Equal(DisplayBlockKind.Blank, blocks[1].Kind);
        Assert.Equal(DisplayBlockKind.Text, blocks[2].Kind);
        Assert.Equal("right", blocks[2].Alignment);
        Assert.Equal(template, DisplayBlockMarkup.Serialize(blocks));
    }
}
