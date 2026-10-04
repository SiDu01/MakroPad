using MakroPad.App.ViewModels;
using MakroPad.Core.Configuration;

namespace MakroPad.App.Tests.ViewModels;

public sealed class DisplaySettingsViewModelTests
{
    [Fact]
    public void LoadCopiesRuntimeSettings()
    {
        var viewModel = new DisplaySettingsViewModel();
        var config = MakroPadConfig.CreateDefault() with
        {
            Hardware = MakroPadConfig.CreateDefault().Hardware with
            {
                Display = MakroPadConfig.CreateDefault().Hardware.Display with { ActionResultTimeoutMs = 1500 }
            },
            AppSettings = MakroPadConfig.CreateDefault().AppSettings with
            {
                EnableNowPlayingDisplay = false,
                VolumeSliderDeadbandPercent = 7
            }
        };

        viewModel.Load(config);

        Assert.False(viewModel.EnableNowPlayingDisplay);
        Assert.Equal(7, viewModel.VolumeSliderDeadbandPercent);
        Assert.Equal(1500, viewModel.ActionResultTimeoutMs);
    }

    [Fact]
    public void DeadbandIsNormalizedAndRaisesChanged()
    {
        var viewModel = new DisplaySettingsViewModel();
        var changedCount = 0;
        viewModel.Changed += (_, _) => changedCount++;

        viewModel.VolumeSliderDeadbandPercent = 99;

        Assert.Equal(25, viewModel.VolumeSliderDeadbandPercent);
        Assert.True(changedCount > 0);
    }

    [Fact]
    public void ApplyToWritesRuntimeSettings()
    {
        var viewModel = new DisplaySettingsViewModel
        {
            ActionResultTimeoutMs = 1234,
            EnableNowPlayingDisplay = false,
            VolumeSliderDeadbandPercent = 4
        };

        var config = viewModel.ApplyTo(MakroPadConfig.CreateDefault(), "COM9");

        Assert.Equal(1234, config.Hardware.Display.ActionResultTimeoutMs);
        Assert.False(config.AppSettings.EnableNowPlayingDisplay);
        Assert.Equal(4, config.AppSettings.VolumeSliderDeadbandPercent);
        Assert.Equal("COM9", config.AppSettings.LastPort);
    }
}
