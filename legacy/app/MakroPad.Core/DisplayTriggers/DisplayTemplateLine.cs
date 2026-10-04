namespace MakroPad.Core.DisplayTriggers;

public sealed record DisplayTemplateLine(string Text, int TextSize = 1, string Alignment = "left")
{
    public int NormalizedTextSize => Math.Clamp(TextSize, 1, 2);

    public string NormalizedAlignment => Alignment?.ToLowerInvariant() switch
    {
        "center" => "center",
        "right" => "right",
        _ => "left"
    };
}
