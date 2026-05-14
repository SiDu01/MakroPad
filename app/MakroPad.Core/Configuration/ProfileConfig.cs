namespace MakroPad.Core.Configuration;

public sealed record ProfileConfig
{
    public string Name { get; init; } = "";

    public List<ActionBinding> Bindings { get; init; } = [];
}
