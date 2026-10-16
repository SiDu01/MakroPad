using Avalonia.Data.Converters;

namespace MakroPad.App.ViewModels;

public static class InspectorConverters
{
    /// <summary>Gives multi-line text fields room for about three lines.</summary>
    public static readonly IValueConverter MultilineHeight =
        new FuncValueConverter<bool, double>(multiline => multiline ? 84 : 0);
}
