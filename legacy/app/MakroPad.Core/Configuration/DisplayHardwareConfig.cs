namespace MakroPad.Core.Configuration;

public sealed record DisplayHardwareConfig
{
    public string Type { get; init; } = "ssd1306";

    public int Width { get; init; } = 128;

    public int Height { get; init; } = 64;

    public string Bus { get; init; } = "i2c";

    public int SdaPin { get; init; } = 8;

    public int SclPin { get; init; } = 9;

    public int ActionResultTimeoutMs { get; init; } = 3000;

    public int DefaultLayoutRows { get; init; } = 3;

    public string DefaultTemplateMode { get; init; } = "normal";

    public string DefaultTitle { get; init; } = "MakroPad";

    public int DefaultTitleSize { get; init; } = 1;

    public string DefaultLine1 { get; init; } = "{profile}";

    public int DefaultLine1Size { get; init; } = 1;

    public string DefaultLine2 { get; init; } = "{status}";

    public int DefaultLine2Size { get; init; } = 1;

    public string DefaultLine3 { get; init; } = "";

    public int DefaultLine3Size { get; init; } = 1;

    public string DefaultLine4 { get; init; } = "";

    public int DefaultLine4Size { get; init; } = 1;
}
