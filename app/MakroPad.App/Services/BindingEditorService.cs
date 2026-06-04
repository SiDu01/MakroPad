using System;
using System.Collections.Generic;

using MakroPad.Core.Configuration;

namespace MakroPad.App.Services;

public sealed class BindingEditorService
{
    private readonly ActionDefinitionCatalog actionDefinitions;

    public BindingEditorService(ActionDefinitionCatalog actionDefinitions)
    {
        this.actionDefinitions = actionDefinitions;
    }

    public BindingEditorResult CreateBinding(string? input, string? action, string? parameters)
    {
        var normalizedInput = input?.Trim() ?? "";
        var normalizedAction = action?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(normalizedInput))
        {
            return BindingEditorResult.Failed("Binding input is required.");
        }

        if (!IsSupportedInput(normalizedInput))
        {
            return BindingEditorResult.Failed("Input must look like button:1 or slider:1.");
        }

        if (string.IsNullOrWhiteSpace(normalizedAction))
        {
            return BindingEditorResult.Failed("Binding action is required.");
        }

        if (!actionDefinitions.TryGet(normalizedAction, out var definition))
        {
            return BindingEditorResult.Failed($"Unsupported action: {normalizedAction}");
        }

        var parsedParameters = ParseParameters(parameters, normalizedAction);
        foreach (var requiredParameter in definition.RequiredParameters)
        {
            if (!parsedParameters.ContainsKey(requiredParameter))
            {
                return BindingEditorResult.Failed($"Missing parameter: {requiredParameter}");
            }
        }

        if (normalizedAction.Equals("system.openUrl", StringComparison.OrdinalIgnoreCase)
            && parsedParameters.TryGetValue("url", out var url)
            && (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
        {
            return BindingEditorResult.Failed("URL must start with http:// or https://.");
        }

        var warning = definition.RequiredParameters.Count == 0
            && !normalizedAction.Equals("log", StringComparison.OrdinalIgnoreCase)
            && parsedParameters.Count > 0
                ? $"Parameters are ignored for {normalizedAction}."
                : "";

        return BindingEditorResult.Succeeded(
            new ActionBinding
            {
                Input = normalizedInput,
                Action = normalizedAction,
                Parameters = parsedParameters
            },
            warning);
    }

    public Dictionary<string, string> ParseParameters(string? parameters, string? action)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(parameters))
        {
            return result;
        }

        if (!parameters.Contains('=') && action?.Equals("log", StringComparison.OrdinalIgnoreCase) == true)
        {
            result["message"] = parameters.Trim();
            return result;
        }

        foreach (var part in parameters.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = part.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = part[..separatorIndex].Trim();
            var value = part[(separatorIndex + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(key))
            {
                result[key] = value;
            }
        }

        return result;
    }

    public static bool IsSupportedInput(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var separatorIndex = input.IndexOf(':');
        if (separatorIndex <= 0 || separatorIndex == input.Length - 1)
        {
            return false;
        }

        var inputType = input[..separatorIndex];
        var id = input[(separatorIndex + 1)..];
        return (inputType.Equals("button", StringComparison.OrdinalIgnoreCase)
                || inputType.Equals("slider", StringComparison.OrdinalIgnoreCase))
            && int.TryParse(id, out var numericId)
            && numericId > 0;
    }
}

public sealed record BindingEditorResult(ActionBinding? Binding, string Error, string Warning)
{
    public bool IsSuccess => Binding is not null;

    public static BindingEditorResult Failed(string error) => new(null, error, "");

    public static BindingEditorResult Succeeded(ActionBinding binding, string warning = "") =>
        new(binding, "", warning);
}
