namespace MakroPad.Core.Configuration;

public sealed record SliderHardwareConfig
{
    public int Id { get; init; }

    public int Pin { get; init; }

    public string Name { get; init; } = "";
}
