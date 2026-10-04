using MakroPad.App.Services;
using MakroPad.App.ViewModels;
using MakroPad.Core.Configuration;

namespace MakroPad.App.Tests.ViewModels;

public sealed class ProfilesEditorViewModelTests
{
    [Fact]
    public void LoadPopulatesProfilesBindingsAndActionEditor()
    {
        var viewModel = CreateViewModel();

        viewModel.Load(CreateConfig());

        Assert.Equal(["Default", "Studio"], viewModel.ProfileNames);
        Assert.Equal("Default", viewModel.ActiveProfileName);
        Assert.Equal("button:1", viewModel.BindingEditorInput);
        Assert.Equal("log", viewModel.ActionEditor.Action);
        Assert.Equal("message=Hello", viewModel.ActionEditor.Parameters);
    }

    [Fact]
    public void ActiveProfileChangeExportsUpdatedConfigAndRaisesEvent()
    {
        var viewModel = CreateViewModel();
        ProfilesEditorChangedEventArgs? changed = null;
        viewModel.Changed += (_, args) => changed = args;
        viewModel.Load(CreateConfig());

        viewModel.ActiveProfileName = "Studio";

        Assert.NotNull(changed);
        Assert.True(changed.ActiveProfileChanged);
        Assert.Equal("Studio", changed.Config.ActiveProfile);
        Assert.Equal("slider:1", viewModel.BindingEditorInput);
    }

    [Fact]
    public void AddBindingUpdatesActiveProfile()
    {
        var viewModel = CreateViewModel();
        viewModel.Load(CreateConfig());
        viewModel.BindingEditorInput = "button:2";
        viewModel.ActionEditor.Load("media.playPause", "");

        viewModel.AddBindingCommand.Execute(null);

        var activeProfile = viewModel.ExportConfig().Profiles.Single(profile => profile.Name == "Default");
        Assert.Contains(activeProfile.Bindings, binding =>
            binding.Input == "button:2" && binding.Action == "media.playPause");
    }

    [Fact]
    public void ApplyBindingRejectsInvalidInput()
    {
        var viewModel = CreateViewModel();
        viewModel.Load(CreateConfig());
        viewModel.BindingEditorInput = "button:";

        viewModel.ApplyBindingCommand.Execute(null);

        Assert.Equal("Input must look like button:1 or slider:1.", viewModel.BindingEditorValidationMessage);
    }

    [Fact]
    public void AddProfileUpdatesProfileListAndActiveProfile()
    {
        var viewModel = CreateViewModel();
        viewModel.Load(CreateConfig());
        viewModel.ProfileEditorName = "Gaming";

        viewModel.AddProfileCommand.Execute(null);

        Assert.Contains("Gaming", viewModel.ProfileNames);
        Assert.Equal("Gaming", viewModel.ActiveProfileName);
        Assert.Equal("Gaming", viewModel.ExportConfig().ActiveProfile);
    }

    private static ProfilesEditorViewModel CreateViewModel()
    {
        var catalog = new ActionDefinitionCatalog();
        var bindingEditorService = new BindingEditorService(catalog);
        return new ProfilesEditorViewModel(
            new ProfileConfigService(),
            bindingEditorService,
            catalog,
            []);
    }

    private static MakroPadConfig CreateConfig()
    {
        return MakroPadConfig.CreateDefault() with
        {
            ActiveProfile = "Default",
            Profiles =
            [
                new ProfileConfig
                {
                    Name = "Default",
                    Bindings =
                    [
                        new ActionBinding
                        {
                            Input = "button:1",
                            Action = "log",
                            Parameters = new Dictionary<string, string>
                            {
                                ["message"] = "Hello"
                            }
                        }
                    ]
                },
                new ProfileConfig
                {
                    Name = "Studio",
                    Bindings =
                    [
                        new ActionBinding
                        {
                            Input = "slider:1",
                            Action = "system.volume"
                        }
                    ]
                }
            ]
        };
    }
}
