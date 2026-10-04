using MakroPad.Core.Actions;

namespace MakroPad.Core.Runtime;

/// <summary>Fixed bindings until profiles arrive in M2.</summary>
public static class DefaultBindings
{
    public static IReadOnlyList<Binding> All { get; } =
    [
        new Binding("button:1", "media.playPause"),
        new Binding("slider:1", "system.volume")
    ];
}
