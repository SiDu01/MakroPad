using MakroPad.App.ViewModels;
using MakroPad.Core.Configuration;

namespace MakroPad.App.Tests.ViewModels;

public sealed class DisplayTriggersEditorViewModelTests
{
    [Fact]
    public void LoadPopulatesSelectionAndPreview()
    {
        var viewModel = new DisplayTriggersEditorViewModel();

        viewModel.Load(
        [
            new DisplayTriggerConfig
            {
                Enabled = true,
                EventType = "input",
                Input = "slider:1",
                Template = "Slider {slider.percent}%"
            }
        ]);

        Assert.NotNull(viewModel.SelectedDisplayTrigger);
        Assert.Equal("slider:1", viewModel.DisplayTriggerEditorInput);
        Assert.Equal("Slider 50%", viewModel.DisplayTriggerPreviewLine1);
    }

    [Fact]
    public void LayoutModePopulatesStructuredPreview()
    {
        var viewModel = new DisplayTriggersEditorViewModel();

        viewModel.Load(
        [
            new DisplayTriggerConfig
            {
                Enabled = true,
                EventType = "input",
                Input = "slider:1",
                TemplateMode = "layout",
                Template = "Volume\nSystem"
            }
        ]);

        Assert.True(viewModel.UsesLayoutDisplayPreview);
        Assert.False(viewModel.UsesTextDisplayPreview);
        Assert.Equal("Volume", viewModel.DisplayTriggerLayoutPreviewTitle);
        Assert.Equal("System", viewModel.DisplayTriggerLayoutPreviewLine1);
        Assert.Equal("50%", viewModel.DisplayTriggerLayoutPreviewValueText);
        Assert.True(viewModel.ShowsDisplayTriggerLayoutPreviewValue);
    }

    [Fact]
    public void TryCommitPendingEditUpdatesExportAndRaisesChanged()
    {
        var viewModel = new DisplayTriggersEditorViewModel();
        var changedCount = 0;
        viewModel.Changed += (_, _) => changedCount++;
        viewModel.Load(
        [
            new DisplayTriggerConfig
            {
                Enabled = true,
                EventType = "input",
                Input = "button:1",
                Template = "Old"
            }
        ]);
        viewModel.DisplayTriggerEditorTemplate = "New {input}";

        Assert.True(viewModel.TryCommitPendingEdit());

        var exported = Assert.Single(viewModel.Export());
        Assert.Equal("New {input}", exported.Template);
        Assert.Equal(1, changedCount);
    }

    [Fact]
    public void TryCommitPendingEditRejectsInvalidInput()
    {
        var viewModel = new DisplayTriggersEditorViewModel();
        viewModel.Load(
        [
            new DisplayTriggerConfig
            {
                Enabled = true,
                EventType = "input",
                Input = "button:1",
                Template = "Input {input}"
            }
        ]);
        viewModel.DisplayTriggerEditorInput = "button:";

        Assert.False(viewModel.TryCommitPendingEdit());
        Assert.Equal("Input must be * or look like button:1 or slider:1.", viewModel.DisplayTriggerEditorValidationMessage);
    }

    [Fact]
    public void TestCommandRaisesValidatedTrigger()
    {
        var viewModel = new DisplayTriggersEditorViewModel();
        DisplayTriggerConfig? requestedTrigger = null;
        viewModel.TestRequested += (_, args) => requestedTrigger = args.Trigger;
        viewModel.Load(
        [
            new DisplayTriggerConfig
            {
                Enabled = true,
                EventType = "profileChanged",
                Input = "*",
                Template = "Profile {profile}"
            }
        ]);

        viewModel.TestDisplayTriggerCommand.Execute(null);

        Assert.NotNull(requestedTrigger);
        Assert.Equal("profileChanged", requestedTrigger.EventType);
        Assert.Equal("Profile {profile}", requestedTrigger.Template);
    }
}
