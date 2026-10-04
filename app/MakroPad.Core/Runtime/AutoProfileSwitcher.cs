using MakroPad.Core.Configuration;

namespace MakroPad.Core.Runtime;

/// <summary>
/// Activates profiles from <see cref="AutoProfileSettings"/> when the foreground program changes.
/// It only acts on a change, so a profile picked by hand stays until the user switches programs.
/// The app's own window is ignored, otherwise opening the editor would change the profile being edited.
/// </summary>
public sealed class AutoProfileSwitcher
{
    private readonly ProfileManager profiles;
    private readonly HashSet<string> ignoredProcesses;
    private AutoProfileSettings settings = new();
    private string? lastProcess;

    public AutoProfileSwitcher(ProfileManager profiles, IEnumerable<string> ignoredProcesses)
    {
        this.profiles = profiles;
        this.ignoredProcesses = new HashSet<string>(ignoredProcesses, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Raised after a switch with the program that caused it and the new profile.</summary>
    public event Action<string, string>? Switched;

    public void Apply(AutoProfileSettings value) => settings = value;

    /// <summary>Returns the profile the program maps to, or null for "keep the current one".</summary>
    public string? TargetFor(string process)
    {
        var name = ConfigEditor.NormalizeProcess(process);
        var rule = settings.Rules.FirstOrDefault(r => string.Equals(ConfigEditor.NormalizeProcess(r.Process), name, StringComparison.OrdinalIgnoreCase));
        return rule?.Profile ?? (string.IsNullOrWhiteSpace(settings.FallbackProfile) ? null : settings.FallbackProfile);
    }

    public void OnForegroundChanged(string process)
    {
        var name = ConfigEditor.NormalizeProcess(process);
        if (!settings.Enabled || name.Length == 0 || ignoredProcesses.Contains(name)
            || string.Equals(name, lastProcess, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        lastProcess = name;
        if (TargetFor(name) is { } target
            && !string.Equals(target, profiles.Active.Name, StringComparison.OrdinalIgnoreCase)
            && profiles.Set(target))
        {
            Switched?.Invoke(name, target);
        }
    }
}
