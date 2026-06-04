namespace MakroPad.App.ViewModels;

public sealed record DisplayTriggerPreviewContext(
    string Input,
    int? SliderValue,
    int? Percent,
    string ActionStatus,
    string ActionName,
    string ActionMessage,
    string MediaArtist,
    string MediaTitle,
    string ConnectionStatus);
