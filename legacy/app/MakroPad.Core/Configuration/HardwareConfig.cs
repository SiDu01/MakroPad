namespace MakroPad.Core.Configuration;

public sealed record HardwareConfig
{
    public List<ButtonHardwareConfig> Buttons { get; init; } = [];

    public List<SliderHardwareConfig> Sliders { get; init; } = [];

    public DisplayHardwareConfig Display { get; init; } = new();
}
