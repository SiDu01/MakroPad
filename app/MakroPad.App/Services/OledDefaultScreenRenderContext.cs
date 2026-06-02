using System;

namespace MakroPad.App.Services;

public sealed record OledDefaultScreenRenderContext(
    string ActiveProfileName,
    string ConnectionStatus,
    NowPlayingTrack? NowPlayingTrack,
    int? SystemVolumePercent,
    DateTime Timestamp);
