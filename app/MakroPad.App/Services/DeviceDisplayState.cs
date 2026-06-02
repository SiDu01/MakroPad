namespace MakroPad.App.Services;

public sealed record DeviceDisplayState(
    string ActiveProfileName,
    string ConnectionStatus,
    int ActionResultTimeoutMs,
    int DefaultScreenRows,
    string DefaultScreenTitle,
    string DefaultScreenLine1,
    string DefaultScreenLine2,
    string DefaultScreenLine3,
    string DefaultScreenLine4,
    bool EnableNowPlayingDisplay,
    NowPlayingTrack? NowPlayingTrack);
