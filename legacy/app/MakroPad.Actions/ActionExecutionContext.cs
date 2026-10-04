using MakroPad.Core.Configuration;

namespace MakroPad.Actions;

public sealed record ActionExecutionContext(
    string Input,
    ActionBinding Binding,
    string? Value = null);
