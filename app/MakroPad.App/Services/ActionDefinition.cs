using System.Collections.Generic;

namespace MakroPad.App.Services;

public sealed record ActionDefinition(
    string Id,
    string Description,
    string ParameterHint,
    string ParameterPlaceholder,
    IReadOnlyList<string> RequiredParameters,
    string? StructuredParameterKey = null,
    string StructuredParameterLabel = "Parameter");
