namespace MakroPad.Core.DisplayTriggers;

public sealed record DisplayTemplateLine(string Text, int TextSize = 1)
{
    public int NormalizedTextSize => Math.Clamp(TextSize, 1, 2);
}
