using MakroPad.Core.Configuration;

namespace MakroPad.Core.Runtime;

/// <summary>Holds the profiles of the current config and which one is active.</summary>
public sealed class ProfileManager
{
    private readonly object gate = new();
    private IReadOnlyList<ProfileConfig> profiles = [];

    public ProfileConfig Active { get; private set; } = new() { Name = "Default" };

    public IReadOnlyList<ProfileConfig> Profiles
    {
        get
        {
            lock (gate)
            {
                return profiles;
            }
        }
    }

    /// <summary>Raised after the active profile changed. The flag is true for explicit user switches.</summary>
    public event Action<ProfileConfig, bool>? ActiveChanged;

    /// <summary>
    /// Loads a new profile list. Keeps the active profile by name if it still exists, otherwise uses
    /// <paramref name="preferred"/> or the first profile.
    /// </summary>
    public void Load(IReadOnlyList<ProfileConfig> newProfiles, string? preferred = null)
    {
        ProfileConfig next;
        lock (gate)
        {
            profiles = newProfiles.Count > 0 ? newProfiles : [new ProfileConfig { Name = "Default" }];
            next = Find(preferred) ?? Find(Active.Name) ?? profiles[0];
            Active = next;
        }

        ActiveChanged?.Invoke(next, false);
    }

    public bool Set(string name)
    {
        ProfileConfig? next;
        lock (gate)
        {
            next = Find(name);
            if (next is null)
            {
                return false;
            }

            Active = next;
        }

        ActiveChanged?.Invoke(next, true);
        return true;
    }

    public ProfileConfig Next()
    {
        ProfileConfig next;
        lock (gate)
        {
            var index = profiles.ToList().FindIndex(p => string.Equals(p.Name, Active.Name, StringComparison.OrdinalIgnoreCase));
            next = profiles[(index + 1) % profiles.Count];
            Active = next;
        }

        ActiveChanged?.Invoke(next, true);
        return next;
    }

    private ProfileConfig? Find(string? name)
        => name is null ? null : profiles.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
}
