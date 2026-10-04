namespace MakroPad.Core.Configuration;

public sealed class ProfileConfigService
{
    public ProfileConfigResult<string> ValidateProfileName(string? name)
    {
        var normalizedName = name?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return ProfileConfigResult<string>.Failure(
                ProfileConfigErrorCode.ProfileNameRequired,
                "Profile name is required.");
        }

        if (normalizedName.Contains(';') || normalizedName.Contains('='))
        {
            return ProfileConfigResult<string>.Failure(
                ProfileConfigErrorCode.ProfileNameContainsReservedCharacter,
                "Profile name cannot contain ';' or '='.");
        }

        return ProfileConfigResult<string>.Success(normalizedName);
    }

    public ProfileConfigResult<ProfileConfig> FindActiveProfile(MakroPadConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var activeProfile = config.Profiles.FirstOrDefault(
            candidate => NamesEqual(candidate.Name, config.ActiveProfile));

        return activeProfile is null
            ? ProfileConfigResult<ProfileConfig>.Failure(
                ProfileConfigErrorCode.ActiveProfileNotFound,
                "Active profile not found.")
            : ProfileConfigResult<ProfileConfig>.Success(activeProfile);
    }

    public ProfileConfigResult<MakroPadConfig> AddProfile(MakroPadConfig config, string? name)
    {
        ArgumentNullException.ThrowIfNull(config);

        var nameResult = ValidateProfileName(name);
        if (!nameResult.IsSuccess)
        {
            return FailureFrom<MakroPadConfig, string>(nameResult);
        }

        var normalizedName = nameResult.Value!;
        if (config.Profiles.Any(profile => NamesEqual(profile.Name, normalizedName)))
        {
            return ProfileConfigResult<MakroPadConfig>.Failure(
                ProfileConfigErrorCode.ProfileAlreadyExists,
                $"Profile already exists: {normalizedName}");
        }

        return ProfileConfigResult<MakroPadConfig>.Success(config with
        {
            Profiles = [.. config.Profiles, new ProfileConfig { Name = normalizedName }],
            ActiveProfile = normalizedName
        });
    }

    public ProfileConfigResult<MakroPadConfig> RenameActiveProfile(MakroPadConfig config, string? name)
    {
        ArgumentNullException.ThrowIfNull(config);

        var nameResult = ValidateProfileName(name);
        if (!nameResult.IsSuccess)
        {
            return FailureFrom<MakroPadConfig, string>(nameResult);
        }

        var activeProfileResult = FindActiveProfile(config);
        if (!activeProfileResult.IsSuccess)
        {
            return FailureFrom<MakroPadConfig, ProfileConfig>(activeProfileResult);
        }

        var activeProfile = activeProfileResult.Value!;
        var normalizedName = nameResult.Value!;
        if (NamesEqual(activeProfile.Name, normalizedName))
        {
            return ProfileConfigResult<MakroPadConfig>.Success(config with
            {
                Profiles = [.. config.Profiles]
            });
        }

        if (config.Profiles.Any(profile => NamesEqual(profile.Name, normalizedName)))
        {
            return ProfileConfigResult<MakroPadConfig>.Failure(
                ProfileConfigErrorCode.ProfileAlreadyExists,
                $"Profile already exists: {normalizedName}");
        }

        return ProfileConfigResult<MakroPadConfig>.Success(config with
        {
            ActiveProfile = normalizedName,
            Profiles = config.Profiles
                .Select(profile => NamesEqual(profile.Name, activeProfile.Name)
                    ? profile with { Name = normalizedName }
                    : profile)
                .ToList()
        });
    }

    public ProfileConfigResult<MakroPadConfig> DeleteActiveProfile(MakroPadConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (config.Profiles.Count <= 1)
        {
            return ProfileConfigResult<MakroPadConfig>.Failure(
                ProfileConfigErrorCode.AtLeastOneProfileRequired,
                "At least one profile is required.");
        }

        var activeProfileResult = FindActiveProfile(config);
        if (!activeProfileResult.IsSuccess)
        {
            return FailureFrom<MakroPadConfig, ProfileConfig>(activeProfileResult);
        }

        var activeProfile = activeProfileResult.Value!;
        var remainingProfiles = config.Profiles
            .Where(profile => !NamesEqual(profile.Name, activeProfile.Name))
            .ToList();

        return ProfileConfigResult<MakroPadConfig>.Success(config with
        {
            ActiveProfile = remainingProfiles[0].Name,
            Profiles = remainingProfiles
        });
    }

    public ProfileConfigResult<MakroPadConfig> UpdateActiveProfile(
        MakroPadConfig config,
        ProfileConfig updatedProfile)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(updatedProfile);

        var activeProfileResult = FindActiveProfile(config);
        if (!activeProfileResult.IsSuccess)
        {
            return FailureFrom<MakroPadConfig, ProfileConfig>(activeProfileResult);
        }

        var activeProfile = activeProfileResult.Value!;
        if (!NamesEqual(activeProfile.Name, updatedProfile.Name))
        {
            return ProfileConfigResult<MakroPadConfig>.Failure(
                ProfileConfigErrorCode.UpdatedProfileNameMismatch,
                "Updated profile name must match the active profile name.");
        }

        return ProfileConfigResult<MakroPadConfig>.Success(config with
        {
            Profiles = config.Profiles
                .Select(profile => NamesEqual(profile.Name, activeProfile.Name)
                    ? updatedProfile
                    : profile)
                .ToList()
        });
    }

    private static bool NamesEqual(string left, string right)
    {
        return left.Equals(right, StringComparison.OrdinalIgnoreCase);
    }

    private static ProfileConfigResult<TTarget> FailureFrom<TTarget, TSource>(
        ProfileConfigResult<TSource> result)
    {
        return ProfileConfigResult<TTarget>.Failure(result.ErrorCode!.Value, result.ErrorMessage);
    }
}
