using System.Collections.Generic;
using MakroPad.Core.DisplayTriggers;

namespace MakroPad.App.Services;

public sealed record DeviceDisplayState(
    string ActiveProfileName,
    string ConnectionStatus,
    int ActionResultTimeoutMs,
    int DefaultScreenRows,
    IReadOnlyList<DisplayTemplateLine> DefaultLines,
    bool EnableNowPlayingDisplay,
    NowPlayingTrack? NowPlayingTrack);
