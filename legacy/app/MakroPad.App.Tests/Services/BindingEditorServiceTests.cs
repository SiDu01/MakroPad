using MakroPad.App.Services;

namespace MakroPad.App.Tests.Services;

public sealed class BindingEditorServiceTests
{
    private readonly BindingEditorService service = new(new ActionDefinitionCatalog());

    [Fact]
    public void CreateBindingBuildsSupportedAction()
    {
        var result = service.CreateBinding(" slider:1 ", "app.volume", "process=Spotify");

        Assert.True(result.IsSuccess);
        Assert.Equal("slider:1", result.Binding!.Input);
        Assert.Equal("Spotify", result.Binding.Parameters["process"]);
    }

    [Theory]
    [InlineData("", "media.playPause", "", "Binding input is required.")]
    [InlineData("invalid", "media.playPause", "", "Input must look like button:1 or slider:1.")]
    [InlineData("button:1", "", "", "Binding action is required.")]
    [InlineData("button:1", "unknown", "", "Unsupported action: unknown")]
    [InlineData("slider:1", "app.volume", "", "Missing parameter: process")]
    public void CreateBindingReturnsValidationErrors(string input, string action, string parameters, string error)
    {
        var result = service.CreateBinding(input, action, parameters);

        Assert.False(result.IsSuccess);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void CreateBindingValidatesUrl()
    {
        var result = service.CreateBinding("button:1", "system.openUrl", "url=not-a-url");

        Assert.Equal("URL must start with http:// or https://.", result.Error);
    }

    [Fact]
    public void CreateBindingWarnsAboutIgnoredParameters()
    {
        var result = service.CreateBinding("button:1", "media.playPause", "foo=bar");

        Assert.True(result.IsSuccess);
        Assert.Equal("Parameters are ignored for media.playPause.", result.Warning);
    }

    [Fact]
    public void ParseParametersSupportsPlainLogMessage()
    {
        var parameters = service.ParseParameters("Hello MakroPad", "log");

        Assert.Equal("Hello MakroPad", parameters["message"]);
    }

    [Fact]
    public void CatalogProvidesStructuredEditorMetadata()
    {
        var catalog = new ActionDefinitionCatalog();

        Assert.True(catalog.TryGet("system.openUrl", out var definition));
        Assert.Equal("url", definition.StructuredParameterKey);
        Assert.Equal("URL", definition.StructuredParameterLabel);
    }
}
