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
}
