namespace MakroPad.Core.DisplayTriggers;

public sealed record DisplayTriggerRenderContext
{
    public string Input { get; init; } = "";

    public int? SliderValue { get; init; }

    public int? SliderPercent { get; init; }

    public string ActionStatus { get; init; } = "";

    public string ActionName { get; init; } = "";

    public string ActionMessage { get; init; } = "";

    public string MediaArtist { get; init; } = "";

    public string MediaTitle { get; init; } = "";

    public string ConnectionStatus { get; init; } = "";

    public string Profile { get; init; } = "";

    public string Status { get; init; } = "";
}
