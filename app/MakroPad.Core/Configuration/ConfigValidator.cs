using System.Text.RegularExpressions;
using MakroPad.Core.Actions;

namespace MakroPad.Core.Configuration;

public sealed record ConfigIssue(string Location, string Message)
{
    public override string ToString() => $"{Location}: {Message}";
}

/// <summary>Checks a config against the hardware layout and the action catalog.</summary>
public static partial class ConfigValidator
{
    public static IReadOnlyList<ConfigIssue> Validate(MakroPadConfig config, int buttonCount = 16, int sliderCount = 3)
    {
        var issues = new List<ConfigIssue>();

        if (config.SchemaVersion != MakroPadConfig.CurrentSchemaVersion)
        {
            issues.Add(new ConfigIssue("schemaVersion", $"Erwartet {MakroPadConfig.CurrentSchemaVersion}, gefunden {config.SchemaVersion}"));
        }

        if (config.Profiles.Count == 0)
        {
            issues.Add(new ConfigIssue("profiles", "Mindestens ein Profil ist nötig"));
        }

        if (config.Settings.LongPressMs is < 200 or > 3000)
        {
            issues.Add(new ConfigIssue("settings.longPressMs", "Muss zwischen 200 und 3000 liegen"));
        }

        if (config.Settings.DoublePressMs is < 100 or > 1000)
        {
            issues.Add(new ConfigIssue("settings.doublePressMs", "Muss zwischen 100 und 1000 liegen"));
        }

        if (config.Settings.SliderDeadbandPercent is < 0 or > 25)
        {
            issues.Add(new ConfigIssue("settings.sliderDeadbandPercent", "Muss zwischen 0 und 25 liegen"));
        }

        if (config.Settings.Theme is not ("system" or "light" or "dark"))
        {
            issues.Add(new ConfigIssue("settings.theme", "Erlaubt sind system, light oder dark"));
        }

        foreach (var input in config.Settings.DisabledInputs)
        {
            // Whole keys only: a disabled key has no long or double press either.
            if (ParseInput(input, buttonCount, sliderCount) is null || input.Count(c => c == ':') != 1)
            {
                issues.Add(new ConfigIssue("settings.disabledInputs", $"Unbekannte Eingabe \"{input}\" (erlaubt: button:1..{buttonCount}, slider:1..{sliderCount})"));
            }
        }

        var ruleProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in config.AutoProfile.Rules)
        {
            var location = $"autoProfile / {rule.Process}";
            if (string.IsNullOrWhiteSpace(rule.Process))
            {
                issues.Add(new ConfigIssue("autoProfile", "Regel ohne Programm"));
            }
            else if (!ruleProcesses.Add(rule.Process.Trim()))
            {
                issues.Add(new ConfigIssue(location, "Für dieses Programm gibt es mehrere Regeln"));
            }

            if (config.FindProfile(rule.Profile) is null)
            {
                issues.Add(new ConfigIssue(location, $"Profil \"{rule.Profile}\" existiert nicht"));
            }
        }

        if (config.AutoProfile.FallbackProfile is { Length: > 0 } fallback && config.FindProfile(fallback) is null)
        {
            issues.Add(new ConfigIssue("autoProfile.fallbackProfile", $"Profil \"{fallback}\" existiert nicht"));
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in config.Profiles)
        {
            var location = $"Profil \"{profile.Name}\"";
            if (string.IsNullOrWhiteSpace(profile.Name))
            {
                issues.Add(new ConfigIssue("profiles", "Profil ohne Namen"));
            }
            else if (!names.Add(profile.Name))
            {
                issues.Add(new ConfigIssue(location, "Name ist doppelt"));
            }

            var inputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var binding in profile.Bindings)
            {
                var bindingLocation = $"{location} / {binding.Input}";
                if (!inputs.Add(binding.Input))
                {
                    issues.Add(new ConfigIssue(bindingLocation, "Eingabe ist mehrfach belegt"));
                }

                issues.AddRange(ValidateBinding(binding, config, buttonCount, sliderCount)
                    .Select(message => new ConfigIssue(bindingLocation, message)));
            }
        }

        return issues;
    }

    private static IEnumerable<string> ValidateBinding(Binding binding, MakroPadConfig config, int buttonCount, int sliderCount)
    {
        var kind = ParseInput(binding.Input, buttonCount, sliderCount);
        if (kind is null)
        {
            yield return $"Unbekannte Eingabe (erlaubt: button:1..{buttonCount}, mit :long oder :double, slider:1..{sliderCount})";
        }

        var definition = ActionCatalog.Find(binding.Action);
        if (definition is null)
        {
            yield return $"Unbekannte Aktion \"{binding.Action}\"";
            yield break;
        }

        if (kind is { } inputKind && !definition.Inputs.HasFlag(inputKind))
        {
            yield return $"{definition.Id} passt nicht zu {(inputKind == InputKinds.Slider ? "einem Slider" : "einer Taste")}";
        }

        foreach (var message in ValidateParameters(definition, binding.Parameters, config))
        {
            yield return message;
        }

        if (!ActionCatalog.HasSteps(definition.Id))
        {
            if (binding.Steps is { Count: > 0 })
            {
                yield return "steps gibt es nur bei den Aktionen macro und toggle";
            }

            yield break;
        }

        var steps = binding.Steps ?? [];
        if (steps.Count == 0)
        {
            yield return "Das Makro hat keine Schritte";
        }
        else if (definition.Id == "toggle" && steps.Count < 2)
        {
            yield return "Abwechselnd braucht mindestens zwei Schritte";
        }
        else if (steps.Count > ActionCatalog.MaxMacroSteps)
        {
            yield return $"Ein Makro hat höchstens {ActionCatalog.MaxMacroSteps} Schritte";
        }

        for (var index = 0; index < steps.Count; index++)
        {
            foreach (var message in ValidateStep(steps[index], config))
            {
                yield return $"Schritt {index + 1}: {message}";
            }
        }
    }

    private static IEnumerable<string> ValidateStep(MacroStep step, MakroPadConfig config)
    {
        var definition = ActionCatalog.MacroStepActions.FirstOrDefault(a => string.Equals(a.Id, step.Action, StringComparison.OrdinalIgnoreCase));
        if (definition is null)
        {
            yield return ActionCatalog.Find(step.Action) is null
                ? $"Unbekannte Aktion \"{step.Action}\""
                : $"{step.Action} geht nicht in einem Makro";
            yield break;
        }

        foreach (var message in ValidateParameters(definition, step.Parameters, config))
        {
            yield return message;
        }

        if (definition == ActionCatalog.Delay && step.GetParameter("ms") is { Length: > 0 } ms
            && (!int.TryParse(ms, out var value) || value < 0 || value > ActionCatalog.MaxDelayMs))
        {
            yield return $"ms muss eine Zahl von 0 bis {ActionCatalog.MaxDelayMs} sein";
        }
    }

    private static IEnumerable<string> ValidateParameters(ActionDefinition definition, IReadOnlyDictionary<string, string>? parameters, MakroPadConfig config)
    {
        string? Get(string key) => parameters is not null && parameters.TryGetValue(key, out var value) ? value : null;

        foreach (var parameter in definition.Parameters.Where(p => p.Required))
        {
            if (string.IsNullOrWhiteSpace(Get(parameter.Name)))
            {
                yield return $"Parameter \"{parameter.Name}\" fehlt (z. B. {parameter.Example})";
            }
        }

        foreach (var key in parameters?.Keys ?? [])
        {
            if (definition.FindParameter(key) is null)
            {
                yield return $"Parameter \"{key}\" wird von {definition.Id} nicht verwendet";
            }
        }

        if (definition.Id is "system.openUrl" or "http.request" && Get("url") is { Length: > 0 } url && !IsHttpUrl(url))
        {
            yield return "url muss eine absolute http- oder https-Adresse sein";
        }

        if (definition.Id == "http.request" && Get("method") is { Length: > 0 } method
            && !HttpMethods.Contains(method.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            yield return "method muss GET, POST, PUT, PATCH oder DELETE sein";
        }

        if (definition.Id == "keyboard.shortcut" && Get("keys") is { Length: > 0 } keys
            && !ShortcutParser.TryParse(keys, out _, out var shortcutError))
        {
            yield return shortcutError;
        }

        if (definition.Id == "profile.set" && Get("name") is { Length: > 0 } name && config.FindProfile(name) is null)
        {
            yield return $"Profil \"{name}\" existiert nicht";
        }
    }

    public static readonly string[] HttpMethods = ["GET", "POST", "PUT", "PATCH", "DELETE"];

    public static bool IsHttpUrl(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    public static InputKinds? ParseInput(string input, int buttonCount, int sliderCount)
    {
        var match = InputPattern().Match(input ?? "");
        if (!match.Success || !int.TryParse(match.Groups[2].Value, out var id) || id < 1)
        {
            return null;
        }

        // Long and double press exist only for keys.
        var isGesture = match.Groups[3].Success;
        return match.Groups[1].Value.ToLowerInvariant() switch
        {
            "button" when id <= buttonCount => InputKinds.Button,
            "slider" when id <= sliderCount && !isGesture => InputKinds.Slider,
            _ => null
        };
    }

    [GeneratedRegex("^(button|slider):([0-9]{1,3})(:long|:double)?$", RegexOptions.IgnoreCase)]
    private static partial Regex InputPattern();
}
