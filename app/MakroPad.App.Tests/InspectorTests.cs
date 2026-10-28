using MakroPad.App.ViewModels;
using MakroPad.Core.Actions;
using MakroPad.Core.Configuration;

namespace MakroPad.App.Tests;

public sealed class InspectorTests
{
    private static readonly ParameterFactory Factory = new(new NoPicker(), new NoRecorder(), () => [], () => []);

    [Fact]
    public void UnknownMacroStepIsSavedUnchanged()
    {
        var step = new MacroStep("keyboard.shortcutt", new Dictionary<string, string> { ["keys"] = "Ctrl+C" });

        var editor = new MacroStepViewModel(step, Factory);

        Assert.Same(step, editor.ToStep());
        Assert.Contains("keyboard.shortcutt", editor.UnknownHint, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownMacroStepIsReplacedOnceEdited()
    {
        var editor = new MacroStepViewModel(new MacroStep("keyboard.shortcutt"), Factory);

        editor.Parameters[0].Value = "300";

        Assert.Equal("delay", editor.ToStep().Action);
        Assert.Null(editor.UnknownHint);
    }

    [Fact]
    public void MultilineTextKeepsSpacesAndLineBreaks()
    {
        var text = new TextParameterViewModel(new ParameterDefinition("text", true, "", ""), multiline: true) { Value = " Gruß\n" };
        var url = new TextParameterViewModel(new ParameterDefinition("url", true, "", "")) { Value = "\thttp://ha.local " };
        var blank = new TextParameterViewModel(new ParameterDefinition("label", false, "", "")) { Value = "  " };

        var values = ParameterFactory.Collect([text, url, blank]);

        Assert.NotNull(values);
        Assert.Equal(" Gruß\n", values["text"]);
        Assert.Equal("http://ha.local", values["url"]);
        Assert.False(values.ContainsKey("label"));
    }

    private sealed class NoPicker : IFilePicker
    {
        public Task<string?> PickProgramAsync() => Task.FromResult<string?>(null);
    }

    private sealed class NoRecorder : IShortcutRecorder
    {
        public Task<string?> RecordAsync() => Task.FromResult<string?>(null);

        public void Cancel()
        {
        }
    }
}
