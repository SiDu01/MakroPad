using MakroPad.App.Services;
using MakroPad.App.ViewModels;

namespace MakroPad.App.Tests.ViewModels;

public sealed class ActionEditorViewModelTests
{
    [Fact]
    public void LoadPopulatesActionParametersAndStructuredValue()
    {
        var viewModel = CreateViewModel();

        viewModel.Load("system.openUrl", "url=https://example.com");

        Assert.Equal("system.openUrl", viewModel.Action);
        Assert.Equal("url=https://example.com", viewModel.Parameters);
        Assert.Equal("https://example.com", viewModel.StructuredParameterValue);
        Assert.Equal("URL", viewModel.StructuredParameterLabel);
        Assert.True(viewModel.ShowsStructuredTextInput);
    }

    [Fact]
    public void StructuredParameterValueUpdatesRawParameters()
    {
        var viewModel = CreateViewModel();
        viewModel.Load("system.openUrl", "");

        viewModel.StructuredParameterValue = " https://makropad.example/path ";

        Assert.Equal("url=https://makropad.example/path", viewModel.Parameters);
    }

    [Fact]
    public void RawParametersUpdateStructuredParameterValue()
    {
        var viewModel = CreateViewModel();
        viewModel.Load("system.openApp", "");

        viewModel.Parameters = "path=notepad.exe";

        Assert.Equal("notepad.exe", viewModel.StructuredParameterValue);
    }

    [Theory]
    [InlineData("not-a-url", "URL must start with http:// or https://.")]
    [InlineData("ftp://example.com", "URL must start with http:// or https://.")]
    [InlineData("https://example.com", "")]
    public void UrlValidationReportsInvalidStructuredValues(string url, string expectedMessage)
    {
        var viewModel = CreateViewModel();
        viewModel.Load("system.openUrl", "");

        viewModel.StructuredParameterValue = url;

        Assert.Equal(expectedMessage, viewModel.StructuredParameterValidationMessage);
    }

    [Fact]
    public void AppVolumeValidationUsesCurrentAudioProcessNames()
    {
        var processNames = new List<string> { "Spotify" };
        var viewModel = CreateViewModel(processNames);
        viewModel.Load("app.volume", "process=spotify");

        Assert.Equal("", viewModel.StructuredParameterValidationMessage);

        viewModel.StructuredParameterValue = "Discord";

        Assert.Equal(
            "Process is not in the current audio session list.",
            viewModel.StructuredParameterValidationMessage);
    }

    [Fact]
    public void ShortcutRecordingCanStartCompleteAndCancel()
    {
        var viewModel = CreateViewModel();

        viewModel.StartShortcutRecordingCommand.Execute(null);

        Assert.Equal("keyboard.shortcut", viewModel.Action);
        Assert.True(viewModel.IsRecordingShortcut);
        Assert.Equal("Press a shortcut. Esc cancels.", viewModel.ShortcutRecordingStatus);

        viewModel.CompleteShortcutRecording("Ctrl+Shift+Esc");

        Assert.False(viewModel.IsRecordingShortcut);
        Assert.Equal("keys=Ctrl+Shift+Esc", viewModel.Parameters);
        Assert.Equal("Ctrl+Shift+Esc", viewModel.StructuredParameterValue);
        Assert.Equal("Recorded: Ctrl+Shift+Esc", viewModel.ShortcutRecordingStatus);

        viewModel.StartShortcutRecordingCommand.Execute(null);
        viewModel.CancelShortcutRecording();

        Assert.False(viewModel.IsRecordingShortcut);
        Assert.Equal("Recording cancelled.", viewModel.ShortcutRecordingStatus);
    }

    [Fact]
    public void SetOpenAppPathSelectsActionAndSynchronizesPath()
    {
        var viewModel = CreateViewModel();

        viewModel.SetOpenAppPath(@"C:\Tools\MakroPad.exe");

        Assert.Equal("system.openApp", viewModel.Action);
        Assert.Equal(@"path=C:\Tools\MakroPad.exe", viewModel.Parameters);
        Assert.Equal(@"C:\Tools\MakroPad.exe", viewModel.StructuredParameterValue);
        Assert.True(viewModel.IsOpenAppAction);
    }

    private static ActionEditorViewModel CreateViewModel(
        IReadOnlyCollection<string>? audioProcessNames = null)
    {
        var catalog = new ActionDefinitionCatalog();
        return new ActionEditorViewModel(
            catalog,
            new BindingEditorService(catalog),
            audioProcessNames ?? Array.Empty<string>());
    }
}
