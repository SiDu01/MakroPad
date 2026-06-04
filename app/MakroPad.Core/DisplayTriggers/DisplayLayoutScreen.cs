namespace MakroPad.Core.DisplayTriggers;

public sealed record DisplayLayoutScreen(
    string Layout,
    string Title,
    string Line1 = "",
    string Line2 = "",
    int? ValuePercent = null,
    string Icon = "")
{
    public string NormalizedLayout => string.IsNullOrWhiteSpace(Layout)
        ? "status"
        : Layout.Trim();

    public int? NormalizedValuePercent => ValuePercent is null
        ? null
        : Math.Clamp(ValuePercent.Value, 0, 100);
}
