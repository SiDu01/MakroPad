using MakroPad.Core.Actions;

namespace MakroPad.Core.Configuration;

/// <summary>Pure edits on an immutable <see cref="MakroPadConfig"/>; each returns a new config.</summary>
public static class ConfigEditor
{
    public static MakroPadConfig SetBinding(MakroPadConfig config, string profileName, Binding binding)
        => UpdateProfile(config, profileName, profile => profile with
        {
            Bindings = Upsert(profile.Bindings, binding)
        });

    public static MakroPadConfig RemoveBinding(MakroPadConfig config, string profileName, string input)
        => UpdateProfile(config, profileName, profile => profile with
        {
            Bindings = [.. profile.Bindings.Where(b => !SameInput(b.Input, input))]
        });

    public static MakroPadConfig AddProfile(MakroPadConfig config, string name, ProfileConfig? copyFrom = null)
    {
        EnsureNewName(config, name);
        var profile = new ProfileConfig { Name = name, Bindings = copyFrom?.Bindings ?? [] };
        return config with { Profiles = [.. config.Profiles, profile] };
    }

    /// <summary>Renames a profile and updates <c>profile.set</c> bindings that point to it.</summary>
    public static MakroPadConfig RenameProfile(MakroPadConfig config, string oldName, string newName)
    {
        if (!string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
        {
            EnsureNewName(config, newName);
        }

        return config with
        {
            Profiles =
            [
                .. config.Profiles.Select(profile => profile with
                {
                    Name = SameName(profile.Name, oldName) ? newName : profile.Name,
                    Bindings = [.. profile.Bindings.Select(binding => RetargetProfileSet(binding, oldName, newName))]
                })
            ]
        };
    }

    public static MakroPadConfig DeleteProfile(MakroPadConfig config, string name)
    {
        if (config.Profiles.Count <= 1)
        {
            throw new InvalidOperationException("Das letzte Profil kann nicht gelöscht werden.");
        }

        return config with { Profiles = [.. config.Profiles.Where(p => !SameName(p.Name, name))] };
    }

    public static MakroPadConfig MoveProfile(MakroPadConfig config, string name, int offset)
    {
        var list = config.Profiles.ToList();
        var index = list.FindIndex(p => SameName(p.Name, name));
        var target = index + offset;
        if (index < 0 || target < 0 || target >= list.Count)
        {
            return config;
        }

        (list[index], list[target]) = (list[target], list[index]);
        return config with { Profiles = list };
    }

    /// <summary>Suggests "Name 2", "Name 3", … until the name is free.</summary>
    public static string UniqueProfileName(MakroPadConfig config, string baseName)
    {
        if (config.FindProfile(baseName) is null)
        {
            return baseName;
        }

        for (var suffix = 2; ; suffix++)
        {
            var candidate = $"{baseName} {suffix}";
            if (config.FindProfile(candidate) is null)
            {
                return candidate;
            }
        }
    }

    private static MakroPadConfig UpdateProfile(MakroPadConfig config, string profileName, Func<ProfileConfig, ProfileConfig> update)
    {
        if (config.FindProfile(profileName) is null)
        {
            throw new InvalidOperationException($"Profil \"{profileName}\" existiert nicht.");
        }

        return config with { Profiles = [.. config.Profiles.Select(p => SameName(p.Name, profileName) ? update(p) : p)] };
    }

    private static IReadOnlyList<Binding> Upsert(IReadOnlyList<Binding> bindings, Binding binding)
    {
        var index = bindings.ToList().FindIndex(b => SameInput(b.Input, binding.Input));
        if (index < 0)
        {
            return [.. bindings, binding];
        }

        var copy = bindings.ToList();
        copy[index] = binding;
        return copy;
    }

    private static Binding RetargetProfileSet(Binding binding, string oldName, string newName)
    {
        if (!string.Equals(binding.Action, "profile.set", StringComparison.OrdinalIgnoreCase)
            || !SameName(binding.GetParameter("name") ?? "", oldName))
        {
            return binding;
        }

        var parameters = new Dictionary<string, string>(binding.Parameters!, StringComparer.OrdinalIgnoreCase) { ["name"] = newName };
        return binding with { Parameters = parameters };
    }

    private static void EnsureNewName(MakroPadConfig config, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Der Profilname darf nicht leer sein.");
        }

        if (config.FindProfile(name) is not null)
        {
            throw new InvalidOperationException($"Ein Profil \"{name}\" gibt es schon.");
        }
    }

    private static bool SameName(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static bool SameInput(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
