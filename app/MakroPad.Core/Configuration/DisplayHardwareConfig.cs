namespace MakroPad.Core.Configuration;

public sealed record DisplayHardwareConfig
{
    public string Type { get; init; } = "ssd1306";

    public int Width { get; init; } = 128;

    public int Height { get; init; } = 64;

    public string Bus { get; init; } = "i2c";

    public int SdaPin { get; init; } = 8;

    public int SclPin { get; init; } = 9;
}
