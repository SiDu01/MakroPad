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

        if (config.Settings.SliderDeadbandPercent is < 0 or > 25)
        {
            issues.Add(new ConfigIssue("settings.sliderDeadbandPercent", "Muss zwischen 0 und 25 liegen"));
        }

        if (config.Settings.Theme is not ("system" or "light" or "dark"))
        {
            issues.Add(new ConfigIssue("settings.theme", "Erlaubt sind system, light oder dark"));
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
            yield return $"Unbekannte Eingabe (erlaubt: button:1..{buttonCount}, slider:1..{sliderCount})";
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

        foreach (var parameter in definition.Parameters.Where(p => p.Required))
        {
            if (string.IsNullOrWhiteSpace(binding.GetParameter(parameter.Name)))
            {
                yield return $"Parameter \"{parameter.Name}\" fehlt (z. B. {parameter.Example})";
            }
        }

        foreach (var key in binding.Parameters?.Keys ?? [])
        {
            if (definition.FindParameter(key) is null)
            {
                yield return $"Parameter \"{key}\" wird von {definition.Id} nicht verwendet";
            }
        }

        if (definition.Id == "system.openUrl" && binding.GetParameter("url") is { Length: > 0 } url && !IsHttpUrl(url))
        {
            yield return "url muss eine absolute http- oder https-Adresse sein";
        }

        if (definition.Id == "keyboard.shortcut" && binding.GetParameter("keys") is { Length: > 0 } keys
            && !ShortcutParser.TryParse(keys, out _, out var shortcutError))
        {
            yield return shortcutError;
        }

        if (definition.Id == "profile.set" && binding.GetParameter("name") is { Length: > 0 } name && config.FindProfile(name) is null)
        {
            yield return $"Profil \"{name}\" existiert nicht";
        }
    }

    public static bool IsHttpUrl(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    public static InputKinds? ParseInput(string input, int buttonCount, int sliderCount)
    {
        var match = InputPattern().Match(input ?? "");
        if (!match.Success || !int.TryParse(match.Groups[2].Value, out var id) || id < 1)
        {
            return null;
        }

        return match.Groups[1].Value.ToLowerInvariant() switch
        {
            "button" when id <= buttonCount => InputKinds.Button,
            "slider" when id <= sliderCount => InputKinds.Slider,
            _ => null
        };
    }

    [GeneratedRegex("^(button|slider):([0-9]{1,3})$", RegexOptions.IgnoreCase)]
    private static partial Regex InputPattern();
}
