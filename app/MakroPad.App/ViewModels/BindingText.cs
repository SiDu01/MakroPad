using MakroPad.Core.Actions;

namespace MakroPad.App.ViewModels;

/// <summary>Short human-readable texts for bindings (keycap legends, summaries).</summary>
public static class BindingText
{
    /// <summary>What to print on a keycap: the user's label, otherwise the most telling parameter.</summary>
    public static string Legend(Binding? binding)
    {
        if (binding is null)
        {
            return "";
        }

        if (binding.GetParameter("label") is { Length: > 0 } label)
        {
            return label;
        }

        return binding.Action switch
        {
            "keyboard.shortcut" => binding.GetParameter("keys") ?? "Tasten",
            "system.openUrl" when Uri.TryCreate(binding.GetParameter("url"), UriKind.Absolute, out var uri) => SiteName(uri.Host),
            "system.openApp" when binding.GetParameter("path") is { Length: > 0 } path => Path.GetFileNameWithoutExtension(path),
            "app.volume" when binding.GetParameter("process") is { Length: > 0 } process => process,
            "profile.set" when binding.GetParameter("name") is { Length: > 0 } name => name,
            "system.volume" => "System",
            "keyboard.text" when binding.GetParameter("text") is { Length: > 0 } text => text.Replace("\\n", " ", StringComparison.Ordinal).Replace('\n', ' '),
            "system.run" when binding.GetParameter("command") is { Length: > 0 } command => MakroPad.Windows.RunCommandAction.Describe(command),
            "http.request" when Uri.TryCreate(binding.GetParameter("url"), UriKind.Absolute, out var target) => SiteName(target.Host),
            "app.volume.foreground" => "Vordergrund",
            "app.volume.others" => "Alle anderen",
            _ => ActionCatalog.Find(binding.Action)?.DisplayName ?? binding.Action
        };
    }

    /// <summary><c>www.youtube.com</c> → <c>youtube</c>: the part people recognise.</summary>
    private static string SiteName(string host)
    {
        var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (parts.Count > 0 && parts[0].Equals("www", StringComparison.OrdinalIgnoreCase))
        {
            parts.RemoveAt(0);
        }

        if (parts.Count > 1)
        {
            parts.RemoveAt(parts.Count - 1);
        }

        return parts.Count > 0 ? string.Join('.', parts) : host;
    }

    public static string GestureHint(bool longBound, bool doubleBound) => (longBound, doubleBound) switch
    {
        (true, true) => "lang, doppelt",
        (true, false) => "lang",
        (false, true) => "doppelt",
        _ => ""
    };

    /// <summary>German field labels for catalog parameters.</summary>
    public static string ParameterLabel(string name) => name switch
    {
        "keys" => "Tasten",
        "process" => "Programm",
        "url" => "Adresse",
        "path" => "Programm oder Datei",
        "args" => "Argumente",
        "name" => "Profil",
        "label" => "Beschriftung",
        "text" => "Text",
        "command" => "Befehl",
        "method" => "Methode",
        "body" => "Inhalt",
        "contentType" => "Inhaltstyp",
        "ms" => "Millisekunden",
        _ => name
    };
}
