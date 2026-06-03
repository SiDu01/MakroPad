using MakroPad.Core.Configuration;

namespace MakroPad.Core.Tests.Configuration;

public sealed class ProfileConfigServiceTests
{
    private readonly ProfileConfigService service = new();

    [Theory]
    [InlineData(" Studio ", "Studio")]
    [InlineData("Default", "Default")]
    public void ValidateProfileNameReturnsTrimmedName(string name, string expected)
    {
        var result = service.ValidateProfileName(name);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value);
        Assert.Null(result.ErrorCode);
        Assert.Empty(result.ErrorMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateProfileNameRejectsMissingName(string? name)
    {
        var result = service.ValidateProfileName(name);

        AssertFailure(result, ProfileConfigErrorCode.ProfileNameRequired, "Profile name is required.");
    }

    [Theory]
    [InlineData("Bad;Name")]
    [InlineData("Bad=Name")]
    [InlineData(" = ")]
    public void ValidateProfileNameRejectsReservedCharacters(string name)
    {
        var result = service.ValidateProfileName(name);

        AssertFailure(
            result,
            ProfileConfigErrorCode.ProfileNameContainsReservedCharacter,
            "Profile name cannot contain ';' or '='.");
    }

    [Fact]
    public void FindActiveProfileUsesCaseInsensitiveComparison()
    {
        var studio = Profile("Studio");
        var config = Config("studio", Profile("Default"), studio);

        var result = service.FindActiveProfile(config);

        Assert.True(result.IsSuccess);
        Assert.Same(studio, result.Value);
    }

    [Fact]
    public void FindActiveProfileReturnsClearFailure()
    {
        var result = service.FindActiveProfile(Config("Missing", Profile("Default")));

        AssertFailure(result, ProfileConfigErrorCode.ActiveProfileNotFound, "Active profile not found.");
    }

    [Fact]
    public void AddProfileAppendsTrimmedProfileAndMakesItActiveWithoutMutatingInput()
    {
        var config = Config("Default", Profile("Default"));

        var result = service.AddProfile(config, " Studio ");

        var updated = AssertSuccess(result);
        Assert.Equal("Studio", updated.ActiveProfile);
        Assert.Equal(["Default", "Studio"], updated.Profiles.Select(profile => profile.Name));
        Assert.Equal("Default", config.ActiveProfile);
        Assert.Single(config.Profiles);
        Assert.NotSame(config.Profiles, updated.Profiles);
    }

    [Fact]
    public void AddProfileRejectsCaseInsensitiveDuplicate()
    {
        var result = service.AddProfile(Config("Default", Profile("Default")), " default ");

        AssertFailure(result, ProfileConfigErrorCode.ProfileAlreadyExists, "Profile already exists: default");
    }

    [Fact]
    public void AddProfilePropagatesNameValidationFailure()
    {
        var result = service.AddProfile(Config("Default", Profile("Default")), " ");

        AssertFailure(result, ProfileConfigErrorCode.ProfileNameRequired, "Profile name is required.");
    }

    [Fact]
    public void RenameActiveProfileRenamesOnlyActiveProfileAndPreservesBindings()
    {
        var active = Profile("Studio", new ActionBinding { Input = "button:1", Action = "media.playPause" });
        var other = Profile("Gaming");
        var config = Config("studio", active, other);

        var result = service.RenameActiveProfile(config, " Work ");

        var updated = AssertSuccess(result);
        Assert.Equal("Work", updated.ActiveProfile);
        Assert.Equal(["Work", "Gaming"], updated.Profiles.Select(profile => profile.Name));
        Assert.Same(active.Bindings, updated.Profiles[0].Bindings);
        Assert.Equal("Studio", config.Profiles[0].Name);
        Assert.NotSame(config.Profiles, updated.Profiles);
    }

    [Fact]
    public void RenameActiveProfileReturnsNewConfigForCaseInsensitiveNoOp()
    {
        var config = Config("Default", Profile("Default"));

        var result = service.RenameActiveProfile(config, "default");

        var updated = AssertSuccess(result);
        Assert.NotSame(config, updated);
        Assert.NotSame(config.Profiles, updated.Profiles);
        Assert.Equal("Default", updated.ActiveProfile);
        Assert.Equal("Default", updated.Profiles[0].Name);
    }

    [Fact]
    public void RenameActiveProfileRejectsDuplicate()
    {
        var result = service.RenameActiveProfile(
            Config("Default", Profile("Default"), Profile("Studio")),
            "studio");

        AssertFailure(result, ProfileConfigErrorCode.ProfileAlreadyExists, "Profile already exists: studio");
    }

    [Fact]
    public void RenameActiveProfileFailsWhenActiveProfileIsMissing()
    {
        var result = service.RenameActiveProfile(Config("Missing", Profile("Default")), "Studio");

        AssertFailure(result, ProfileConfigErrorCode.ActiveProfileNotFound, "Active profile not found.");
    }

    [Fact]
    public void DeleteActiveProfileRemovesActiveAndSelectsFirstRemainingWithoutMutatingInput()
    {
        var config = Config("Studio", Profile("Default"), Profile("Studio"), Profile("Gaming"));

        var result = service.DeleteActiveProfile(config);

        var updated = AssertSuccess(result);
        Assert.Equal("Default", updated.ActiveProfile);
        Assert.Equal(["Default", "Gaming"], updated.Profiles.Select(profile => profile.Name));
        Assert.Equal(3, config.Profiles.Count);
        Assert.NotSame(config.Profiles, updated.Profiles);
    }

    [Fact]
    public void DeleteActiveProfileRejectsDeletingLastProfile()
    {
        var result = service.DeleteActiveProfile(Config("Default", Profile("Default")));

        AssertFailure(
            result,
            ProfileConfigErrorCode.AtLeastOneProfileRequired,
            "At least one profile is required.");
    }

    [Fact]
    public void DeleteActiveProfileFailsWhenActiveProfileIsMissing()
    {
        var result = service.DeleteActiveProfile(Config("Missing", Profile("Default"), Profile("Studio")));

        AssertFailure(result, ProfileConfigErrorCode.ActiveProfileNotFound, "Active profile not found.");
    }

    [Fact]
    public void UpdateActiveProfileReplacesOnlyActiveProfileWithoutMutatingInput()
    {
        var active = Profile("Studio");
        var other = Profile("Gaming");
        var config = Config("studio", active, other);
        var updatedProfile = Profile(
            "Studio",
            new ActionBinding { Input = "slider:1", Action = "system.volume" });

        var result = service.UpdateActiveProfile(config, updatedProfile);

        var updated = AssertSuccess(result);
        Assert.Same(updatedProfile, updated.Profiles[0]);
        Assert.Same(other, updated.Profiles[1]);
        Assert.Same(active, config.Profiles[0]);
        Assert.NotSame(config.Profiles, updated.Profiles);
    }

    [Fact]
    public void UpdateActiveProfileRejectsNameChange()
    {
        var result = service.UpdateActiveProfile(
            Config("Default", Profile("Default")),
            Profile("Studio"));

        AssertFailure(
            result,
            ProfileConfigErrorCode.UpdatedProfileNameMismatch,
            "Updated profile name must match the active profile name.");
    }

    [Fact]
    public void UpdateActiveProfileFailsWhenActiveProfileIsMissing()
    {
        var result = service.UpdateActiveProfile(
            Config("Missing", Profile("Default")),
            Profile("Missing"));

        AssertFailure(result, ProfileConfigErrorCode.ActiveProfileNotFound, "Active profile not found.");
    }

    private static MakroPadConfig Config(string activeProfile, params ProfileConfig[] profiles)
    {
        return new MakroPadConfig
        {
            ActiveProfile = activeProfile,
            Profiles = [.. profiles]
        };
    }

    private static ProfileConfig Profile(string name, params ActionBinding[] bindings)
    {
        return new ProfileConfig
        {
            Name = name,
            Bindings = [.. bindings]
        };
    }

    private static T AssertSuccess<T>(ProfileConfigResult<T> result)
    {
        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorCode);
        Assert.Empty(result.ErrorMessage);
        return Assert.IsType<T>(result.Value);
    }

    private static void AssertFailure<T>(
        ProfileConfigResult<T> result,
        ProfileConfigErrorCode expectedCode,
        string expectedMessage)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Equal(expectedCode, result.ErrorCode);
        Assert.Equal(expectedMessage, result.ErrorMessage);
    }
}
