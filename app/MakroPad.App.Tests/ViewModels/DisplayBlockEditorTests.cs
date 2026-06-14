using MakroPad.App.ViewModels;

namespace MakroPad.App.Tests.ViewModels;

public sealed class DisplayBlockEditorTests
{
    [Fact]
    public void ParsesMarkdownTemplateIntoBlocks()
    {
        var vm = new DisplayTriggersEditorViewModel
        {
            DisplayTriggerEditorTemplate = "# Title\nStatus {status}\n---\n- One"
        };

        vm.ParseBlocksFromTemplate();

        Assert.Equal(4, vm.DisplayBlocks.Count);
        Assert.Equal(DisplayBlockKind.Heading, vm.DisplayBlocks[0].Kind);
        Assert.Equal("Title", vm.DisplayBlocks[0].Text);
        Assert.Equal(DisplayBlockKind.Text, vm.DisplayBlocks[1].Kind);
        Assert.Equal("Status {status}", vm.DisplayBlocks[1].Text);
        Assert.Equal(DisplayBlockKind.Divider, vm.DisplayBlocks[2].Kind);
        Assert.Equal(DisplayBlockKind.ListItem, vm.DisplayBlocks[3].Kind);
        Assert.Equal("One", vm.DisplayBlocks[3].Text);
    }

    [Fact]
    public void EditingABlockUpdatesTheTemplate()
    {
        var vm = new DisplayTriggersEditorViewModel
        {
            DisplayTriggerEditorTemplate = "# Title\n- One"
        };
        vm.ParseBlocksFromTemplate();

        vm.DisplayBlocks[0].Text = "New";

        Assert.Equal("# New\n- One", vm.DisplayTriggerEditorTemplate);
    }
}
