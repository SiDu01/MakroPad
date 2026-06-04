namespace MakroPad.App.Services;

public sealed record DisplayTriggerDispatchResult(
    int MatchedTriggerCount,
    int SentScreenCount,
    bool WasCancelled)
{
    public bool Handled => MatchedTriggerCount > 0;

    public static DisplayTriggerDispatchResult NotHandled { get; } = new(0, 0, false);
}
