namespace MakroPad.Core.Actions;

[Flags]
public enum InputKinds
{
    Button = 1,
    Slider = 2,
    Any = Button | Slider
}

public sealed record ParameterDefinition(string Name, bool Required, string Hint, string Example);

/// <summary>Describes an action for validation, editors and documentation.</summary>
public sealed record ActionDefinition(
    string Id,
    string DisplayName,
    string Category,
    InputKinds Inputs,
    string Description,
    IReadOnlyList<ParameterDefinition> Parameters)
{
    public ParameterDefinition? FindParameter(string name)
        => Parameters.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// The single list of supported actions. Handlers (e.g. in MakroPad.Windows) implement these ids;
/// validation and future editors read the metadata from here.
/// </summary>
public static class ActionCatalog
{
    private static readonly ParameterDefinition[] None = [];

    public static IReadOnlyList<ActionDefinition> All { get; } =
    [
        new("media.playPause", "Play/Pause", "Wiedergabe", InputKinds.Button, "Medientaste Play/Pause", None),
        new("media.nextTrack", "Nächster Titel", "Wiedergabe", InputKinds.Button, "Medientaste Weiter", None),
        new("media.previousTrack", "Vorheriger Titel", "Wiedergabe", InputKinds.Button, "Medientaste Zurück", None),
        new("media.stop", "Stopp", "Wiedergabe", InputKinds.Button, "Medientaste Stopp", None),
        new("media.mute", "Stumm", "Lautstärke", InputKinds.Button, "Systemton stumm schalten", None),
        new("media.volumeUp", "Lauter", "Lautstärke", InputKinds.Button, "Lautstärke-Taste +", None),
        new("media.volumeDown", "Leiser", "Lautstärke", InputKinds.Button, "Lautstärke-Taste −", None),
        new("system.volume", "Systemlautstärke", "Lautstärke", InputKinds.Slider, "Setzt die Lautstärke des Standard-Ausgabegeräts", None),
        new("app.volume", "Programm-Lautstärke", "Lautstärke", InputKinds.Slider, "Setzt die Lautstärke aller Audio-Sessions eines Programms",
        [
            new("process", true, "Prozessname ohne .exe", "Spotify"),
            new("label", false, "Steht auf der Taste und in der Einblendung statt des Programmnamens", "Musik")
        ]),
        new("app.volume.foreground", "Lautstärke im Vordergrund", "Lautstärke", InputKinds.Slider,
            "Setzt die Lautstärke des Programms, das gerade im Vordergrund ist",
        [
            new("label", false, "Steht auf der Beschriftung und in der Einblendung", "Fokus")
        ]),
        new("app.volume.others", "Lautstärke aller anderen", "Lautstärke", InputKinds.Slider,
            "Setzt die Lautstärke aller Programme außer dem im Vordergrund",
        [
            new("label", false, "Steht auf der Beschriftung und in der Einblendung", "Hintergrund")
        ]),
        new("keyboard.shortcut", "Tastenkombination", "Tastatur", InputKinds.Button, "Sendet eine Tastenkombination an das aktive Fenster",
        [
            new("keys", true, "Tasten mit + verbunden: Ctrl, Shift, Alt, Win, A-Z, 0-9, F1-F24, Enter, Esc, Tab, Space, Pfeiltasten …", "Ctrl+Shift+Esc"),
            new("label", false, "Steht auf der Taste und in der Einblendung statt der Tastenkombination", "Task-Manager")
        ]),
        new("keyboard.text", "Text eingeben", "Tastatur", InputKinds.Button, "Tippt einen Text in das aktive Fenster",
        [
            new("text", true, "Beliebiger Text, auch Umlaute und Emojis; \\n wird zu Enter", "Mit freundlichen Grüßen"),
            new("label", false, "Steht auf der Taste und in der Einblendung statt des Texts", "Gruß")
        ]),
        new("system.run", "Befehl ausführen", "Öffnen", InputKinds.Button,
            "Führt einen Befehl oder ein Skript unsichtbar aus und meldet, ob er fehlschlägt",
        [
            new("command", true, "Befehlszeile wie in der Eingabeaufforderung", "powershell -File C:\\Skripte\\backup.ps1"),
            new("label", false, "Steht auf der Taste und in der Einblendung statt des Befehls", "Backup")
        ]),
        new("http.request", "Webanfrage senden", "Öffnen", InputKinds.Button,
            "Sendet eine HTTP-Anfrage, z. B. an Home Assistant oder einen Webhook",
        [
            new("url", true, "Vollständige Adresse mit http:// oder https://", "http://homeassistant.local:8123/api/webhook/licht"),
            new("method", false, "GET, POST, PUT, PATCH oder DELETE (Standard: GET, mit Inhalt POST)", "POST"),
            new("body", false, "Inhalt der Anfrage", "{\"an\": true}"),
            new("contentType", false, "Art des Inhalts (Standard: application/json)", "application/json"),
            new("label", false, "Steht auf der Taste und in der Einblendung statt der Adresse", "Licht")
        ]),
        new("macro", "Makro", "Ablauf", InputKinds.Button, "Führt mehrere Aktionen nacheinander aus, mit Pausen dazwischen",
        [
            new("label", false, "Steht auf der Taste und in der Einblendung", "Stream starten")
        ]),
        new("toggle", "Abwechselnd", "Ablauf", InputKinds.Button, "Jeder Druck führt den nächsten Schritt aus, nach dem letzten wieder den ersten",
        [
            new("label", false, "Steht auf der Taste und in der Einblendung", "Kamera")
        ]),
        new("system.openUrl", "Webseite öffnen", "Öffnen", InputKinds.Button, "Öffnet eine http(s)-Adresse im Standardbrowser",
        [
            new("url", true, "Vollständige Adresse mit https://", "https://www.youtube.com"),
            new("label", false, "Steht auf der Taste und in der Einblendung statt der Adresse", "YouTube")
        ]),
        new("system.openApp", "Programm starten", "Öffnen", InputKinds.Button, "Startet ein Programm oder öffnet eine Datei",
        [
            new("path", true, "Programm, Datei oder Befehl im PATH", "notepad.exe"),
            new("args", false, "Argumente", ""),
            new("label", false, "Steht auf der Taste und in der Einblendung statt des Programmnamens", "Editor")
        ]),
        new("profile.next", "Nächstes Profil", "Profile", InputKinds.Button, "Wechselt zum nächsten Profil in der Liste", None),
        new("profile.set", "Profil wählen", "Profile", InputKinds.Button, "Wechselt zu einem bestimmten Profil",
        [
            new("name", true, "Name des Profils", "Gaming")
        ])
    ];

    /// <summary>Pause between macro steps; only valid inside a macro.</summary>
    public static ActionDefinition Delay { get; } = new("delay", "Warten", "Ablauf", InputKinds.Button,
        "Wartet, bevor der nächste Schritt startet",
        [new ParameterDefinition("ms", true, "Millisekunden, höchstens 60000", "200")]);

    public const int MaxDelayMs = 60000;
    public const int MaxMacroSteps = 50;

    /// <summary>Actions a step may use (button actions except macro and toggle), plus <see cref="Delay"/>.</summary>
    public static IReadOnlyList<ActionDefinition> MacroStepActions { get; } =
        [Delay, .. All.Where(a => a.Inputs.HasFlag(InputKinds.Button) && !HasSteps(a.Id))];

    /// <summary><c>macro</c> and <c>toggle</c> run a list of steps.</summary>
    public static bool HasSteps(string actionId)
        => string.Equals(actionId, "macro", StringComparison.OrdinalIgnoreCase) || string.Equals(actionId, "toggle", StringComparison.OrdinalIgnoreCase);

    public static ActionDefinition? Find(string actionId)
        => All.FirstOrDefault(definition => string.Equals(definition.Id, actionId, StringComparison.OrdinalIgnoreCase));
}
