using MakroPad.Core.Device;

namespace MakroPad.Core.Actions;

/// <summary>Maps an input to an action id plus parameters.</summary>
public sealed record Binding(string Input, string Action, IReadOnlyDictionary<string, string>? Parameters = null)
{
    public string? GetParameter(string key)
        => Parameters is not null && Parameters.TryGetValue(key, out var value) ? value : null;
}

public sealed record ActionContext(Binding Binding, InputEvent Input);

/// <summary>Outcome of an action. <see cref="Percent"/> is set by level-type actions such as volume.</summary>
public sealed record ActionResult(bool Success, string Message, int? Percent = null)
{
    /// <summary>The action shows its own feedback; the runtime skips the generic overlay.</summary>
    public bool Quiet { get; init; }

    public static ActionResult Ok(string message, int? percent = null) => new(true, message, percent);

    public static ActionResult Failed(string message) => new(false, message);
}

public interface IActionHandler
{
    /// <summary>Stable action id used in bindings, e.g. <c>media.playPause</c>.</summary>
    string ActionId { get; }

    Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken);
}
