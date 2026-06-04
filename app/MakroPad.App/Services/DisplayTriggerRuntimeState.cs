namespace MakroPad.App.Services;

public sealed record DisplayTriggerRuntimeState(
    string ActiveProfileName,
    string ConnectionStatus,
    NowPlayingTrack? MediaTrack = null);
