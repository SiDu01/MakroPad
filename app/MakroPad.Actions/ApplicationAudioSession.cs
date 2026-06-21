namespace MakroPad.Actions;

public sealed record ApplicationAudioSession(
    string ProcessName,
    int ProcessId,
    string DisplayName,
    int VolumePercent,
    bool IsMuted,
    bool IsActive = false);
