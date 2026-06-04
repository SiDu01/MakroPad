using MakroPad.Actions;
using MakroPad.App.Services;
using MakroPad.App.ViewModels;
using MakroPad.Core.Configuration;

namespace MakroPad.App.Tests.ViewModels;

public sealed class DisplaySettingsViewModelTests
{
    [Fact]
    public void LoadPopulatesSettingsAndPreview()
    {
        var viewModel = CreateViewModel();
        var config = MakroPadConfig.CreateDefault() with
        {
            Hardware = MakroPadConfig.CreateDefault().Hardware with
            {
                Display = MakroPadConfig.CreateDefault().Hardware.Display with
                {
                    DefaultTemplateMode = "html",
                    DefaultTitle = "Pad",
                    DefaultTitleSize = 2,
                    DefaultLine1 = "{profile}",
                    DefaultLine1Size = 1,
                    DefaultLine2 = "{status}",
                    DefaultLine2Size = 2,
                    DefaultLayoutRows = 5,
                    DefaultLine3 = "{media.artist}",
                    DefaultLine3Size = 1,
                    DefaultLine4 = "{media.title}"
                }
            }
        };

        viewModel.SetRuntime("Studio", "Connected", new NowPlayingTrack("Artist", "Track"));
        viewModel.Load(config);

        Assert.Equal("Pad", viewModel.OledPreviewTitle);
        Assert.Equal("html", viewModel.DefaultScreenTemplateMode);
        Assert.Equal(2, viewModel.DefaultScreenTitleSize);
        Assert.Equal("Studio", viewModel.OledPreviewLine1);
        Assert.Equal("Connected", viewModel.OledPreviewLine2);
        Assert.Equal(2, viewModel.DefaultScreenLine2Size);
        Assert.True(viewModel.UsesFiveLineOledLayout);
        Assert.Equal("Artist", viewModel.OledPreviewLine3);
        Assert.Equal("Track", viewModel.OledPreviewLine4);
    }

    [Fact]
    public void DeadbandIsNormalizedAndRaisesChanged()
    {
        var viewModel = CreateViewModel();
        var changedCount = 0;
        viewModel.Changed += (_, _) => changedCount++;

        viewModel.VolumeSliderDeadbandPercent = 99;

        Assert.Equal(25, viewModel.VolumeSliderDeadbandPercent);
        Assert.True(changedCount > 0);
    }

    [Fact]
    public void ApplyToWritesDisplayAndAppSettings()
    {
        var viewModel = CreateViewModel();
        viewModel.ActionResultTimeoutMs = 1234;
        viewModel.DefaultScreenRows = 5;
        viewModel.DefaultScreenTemplateMode = "html";
        viewModel.DefaultScreenTitle = " Studio ";
        viewModel.DefaultScreenTitleSize = 2;
        viewModel.DefaultScreenLine1 = "";
        viewModel.DefaultScreenLine1Size = 2;
        viewModel.DefaultScreenLine2 = "";
        viewModel.DefaultScreenLine3 = " Three ";
        viewModel.DefaultScreenLine4 = " Four ";
        viewModel.DefaultScreenLine4Size = 99;
        viewModel.EnableNowPlayingDisplay = false;
        viewModel.VolumeSliderDeadbandPercent = 4;

        var config = viewModel.ApplyTo(MakroPadConfig.CreateDefault(), "COM9");

        Assert.Equal(1234, config.Hardware.Display.ActionResultTimeoutMs);
        Assert.Equal(5, config.Hardware.Display.DefaultLayoutRows);
        Assert.Equal("html", config.Hardware.Display.DefaultTemplateMode);
        Assert.Equal("Studio", config.Hardware.Display.DefaultTitle);
        Assert.Equal(2, config.Hardware.Display.DefaultTitleSize);
        Assert.Equal("{profile}", config.Hardware.Display.DefaultLine1);
        Assert.Equal(2, config.Hardware.Display.DefaultLine1Size);
        Assert.Equal("{status}", config.Hardware.Display.DefaultLine2);
        Assert.Equal("Three", config.Hardware.Display.DefaultLine3);
        Assert.Equal("Four", config.Hardware.Display.DefaultLine4);
        Assert.Equal(2, config.Hardware.Display.DefaultLine4Size);
        Assert.False(config.AppSettings.EnableNowPlayingDisplay);
        Assert.Equal(4, config.AppSettings.VolumeSliderDeadbandPercent);
        Assert.Equal("COM9", config.AppSettings.LastPort);
    }

    [Fact]
    public void CreateDeviceDisplayStateUsesRuntimeSnapshot()
    {
        var viewModel = CreateViewModel();

        viewModel.SetRuntime("Gaming", "Connected", new NowPlayingTrack("Artist", "Track"));
        viewModel.DefaultScreenTitleSize = 2;
        var state = viewModel.CreateDeviceDisplayState();

        Assert.Equal("Gaming", state.ActiveProfileName);
        Assert.Equal("Connected", state.ConnectionStatus);
        Assert.Equal(2, state.DefaultScreenTitleSize);
        Assert.Equal("Artist", state.NowPlayingTrack?.Artist);
        Assert.Equal("Track", state.NowPlayingTrack?.Title);
    }

    private static DisplaySettingsViewModel CreateViewModel()
    {
        return new DisplaySettingsViewModel(
            new DeviceDisplaySyncService(new FakeSystemVolumeController()));
    }

    private sealed class FakeSystemVolumeController : ISystemVolumeController
    {
        public Task<int?> GetMasterVolumePercentAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<int?>(42);
        }

        public Task<ActionExecutionResult> SetMasterVolumeAsync(int percent, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
