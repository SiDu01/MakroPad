namespace MakroPad.App.Services;

public sealed record DeviceDisplayState(
    string ActiveProfileName,
    string ConnectionStatus,
    int ActionResultTimeoutMs,
    int DefaultScreenRows,
    string DefaultScreenTemplateMode,
    string DefaultScreenTitle,
    int DefaultScreenTitleSize,
    string DefaultScreenLine1,
    int DefaultScreenLine1Size,
    string DefaultScreenLine2,
    int DefaultScreenLine2Size,
    string DefaultScreenLine3,
    int DefaultScreenLine3Size,
    string DefaultScreenLine4,
    int DefaultScreenLine4Size,
    bool EnableNowPlayingDisplay,
    NowPlayingTrack? NowPlayingTrack);
