namespace MakroPad.Core.Configuration;

public sealed record ActionBinding
{
    public string Input { get; init; } = "";

    public string Action { get; init; } = "";

    public Dictionary<string, string> Parameters { get; init; } = [];
}
