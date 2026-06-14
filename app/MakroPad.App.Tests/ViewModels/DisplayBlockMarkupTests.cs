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
}
