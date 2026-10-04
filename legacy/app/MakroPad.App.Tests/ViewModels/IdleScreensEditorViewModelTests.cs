using System.Linq;
using MakroPad.App.ViewModels;
using MakroPad.Core.Configuration;

namespace MakroPad.App.Tests.ViewModels;

public sealed class IdleScreensEditorViewModelTests
{
    private static MakroPadConfig ConfigWith(params IdleScreenConfig[] screens)
    {
        return MakroPadConfig.CreateDefault() with
        {
            AppSettings = MakroPadConfig.CreateDefault().AppSettings with
            {
                IdleScreens = screens.ToList(),
                IdleRotationSeconds = 12
            }
        };
    }

    [Fact]
    public void LoadAndExportRoundTripsIdleScreens()
    {
        var vm = new IdleScreensEditorViewModel();
        vm.Load(ConfigWith(
            new IdleScreenConfig { Name = "Standard", Activation = "default", Template = "{profile}" },
            new IdleScreenConfig { Name = "Spotify", Activation = "process", ProcessName = "Spotify", Template = "# {media.title}" }));

        Assert.Equal(2, vm.IdleScreens.Count);
        Assert.Equal(12, vm.IdleRotationSeconds);
        Assert.NotNull(vm.SelectedIdleScreen);

        var exported = vm.Export();
        Assert.Equal("Standard", exported[0].Name);
        Assert.Equal("process", exported[1].Activation);
        Assert.Equal("Spotify", exported[1].ProcessName);
    }

    [Fact]
    public void EditingSelectedScreenRaisesChanged()
    {
        var vm = new IdleScreensEditorViewModel();
        vm.Load(ConfigWith(new IdleScreenConfig { Name = "Standard", Template = "{profile}" }));
        var changed = false;
        vm.Changed += (_, _) => changed = true;

        vm.SelectedIdleScreen!.Name = "Umbenannt";

        Assert.True(changed);
        Assert.Equal("Umbenannt", vm.Export()[0].Name);
    }

    [Fact]
    public void LoadAndExportRoundTripsConditionsAndBehavior()
    {
        var vm = new IdleScreensEditorViewModel();
        vm.Load(ConfigWith(new IdleScreenConfig
        {
            Name = "Spotify",
            Template = "# {media.title}",
            ConditionLogic = "or",
            Priority = 20,
            Exclusive = true,
            ActivationDelaySeconds = 2,
            MinDwellSeconds = 5,
            Conditions = [new IdleScreenCondition { Type = "mediaActive", Target = "Spotify" }]
        }));

        var exported = vm.Export()[0];
        Assert.Equal("or", exported.ConditionLogic);
        Assert.Equal(20, exported.Priority);
        Assert.True(exported.Exclusive);
        Assert.Equal(2, exported.ActivationDelaySeconds);
        Assert.Equal(5, exported.MinDwellSeconds);
        var condition = Assert.Single(exported.Conditions);
        Assert.Equal("mediaActive", condition.Type);
        Assert.Equal("Spotify", condition.Target);
    }

    [Fact]
    public void AddAndRemoveConditionUpdatesExportAndRaisesChanged()
    {
        var vm = new IdleScreensEditorViewModel();
        vm.Load(ConfigWith(new IdleScreenConfig { Name = "Standard", Template = "{profile}" }));
        var changes = 0;
        vm.Changed += (_, _) => changes++;

        vm.AddConditionCommand.Execute(null);
        Assert.Single(vm.SelectedIdleScreen!.Conditions);
        Assert.Single(vm.Export()[0].Conditions);
        Assert.True(changes > 0);

        var condition = vm.SelectedIdleScreen.Conditions[0];
        vm.RemoveConditionCommand.Execute(condition);
        Assert.Empty(vm.SelectedIdleScreen.Conditions);
        Assert.Empty(vm.Export()[0].Conditions);
    }
}
