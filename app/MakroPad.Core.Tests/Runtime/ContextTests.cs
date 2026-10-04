using MakroPad.Core.Configuration;
using MakroPad.Core.Runtime;

namespace MakroPad.Core.Tests.Runtime;

public sealed class AutoProfileSwitcherTests
{
    private readonly ProfileManager profiles = new();
    private readonly AutoProfileSwitcher switcher;

    public AutoProfileSwitcherTests()
    {
        profiles.Load([new ProfileConfig { Name = "Default" }, new ProfileConfig { Name = "Discord" }, new ProfileConfig { Name = "Game" }]);
        switcher = new AutoProfileSwitcher(profiles, ["MakroPad.App"]);
        switcher.Apply(new AutoProfileSettings
        {
            Rules = [new ProfileRule("Discord", "Discord"), new ProfileRule("eldenring.exe", "Game")]
        });
    }

    [Fact]
    public void RuleActivatesProfile()
    {
        switcher.OnForegroundChanged("Discord");
        Assert.Equal("Discord", profiles.Active.Name);

        switcher.OnForegroundChanged("eldenring");
        Assert.Equal("Game", profiles.Active.Name);
    }

    [Fact]
    public void WithoutFallbackOtherProgramsKeepTheProfile()
    {
        switcher.OnForegroundChanged("Discord");
        switcher.OnForegroundChanged("notepad");
        Assert.Equal("Discord", profiles.Active.Name);
    }

    [Fact]
    public void FallbackAppliesToProgramsWithoutRule()
    {
        switcher.Apply(new AutoProfileSettings { Rules = [new ProfileRule("Discord", "Discord")], FallbackProfile = "Default" });
        switcher.OnForegroundChanged("Discord");
        switcher.OnForegroundChanged("chrome");
        Assert.Equal("Default", profiles.Active.Name);
    }

    [Fact]
    public void ManualChoiceStaysUntilTheProgramChanges()
    {
        switcher.OnForegroundChanged("Discord");
        profiles.Set("Game");

        switcher.OnForegroundChanged("Discord");
        Assert.Equal("Game", profiles.Active.Name);

        switcher.OnForegroundChanged("notepad");
        switcher.OnForegroundChanged("Discord");
        Assert.Equal("Discord", profiles.Active.Name);
    }

    [Fact]
    public void OwnWindowAndDisabledSettingsAreIgnored()
    {
        switcher.Apply(new AutoProfileSettings { Rules = [new ProfileRule("Discord", "Discord")], FallbackProfile = "Game" });
        switcher.OnForegroundChanged("MakroPad.App");
        Assert.Equal("Default", profiles.Active.Name);

        switcher.Apply(new AutoProfileSettings { Enabled = false, Rules = [new ProfileRule("Discord", "Discord")] });
        switcher.OnForegroundChanged("Discord");
        Assert.Equal("Default", profiles.Active.Name);
    }

    [Fact]
    public void SwitchedReportsProgramAndProfile()
    {
        var reports = new List<(string, string)>();
        switcher.Switched += (process, profile) => reports.Add((process, profile));

        switcher.OnForegroundChanged("Discord.exe");
        switcher.OnForegroundChanged("notepad");

        Assert.Equal([("Discord", "Discord")], reports);
    }
}

public sealed class ProfileRuleEditingTests
{
    private static readonly MakroPadConfig Config = new()
    {
        Profiles = [new ProfileConfig { Name = "Default" }, new ProfileConfig { Name = "Discord" }],
        AutoProfile = new AutoProfileSettings { Rules = [new ProfileRule("Discord", "Discord")], FallbackProfile = "Discord" }
    };

    [Fact]
    public void SetRuleNormalizesAndReplaces()
    {
        var result = ConfigEditor.SetProfileRule(Config, new ProfileRule(" discord.exe ", "Default"));

        var rule = Assert.Single(result.AutoProfile.Rules);
        Assert.Equal(new ProfileRule("discord", "Default"), rule);
    }

    [Fact]
    public void InvalidRulesAreRejected()
    {
        Assert.Throws<InvalidOperationException>(() => ConfigEditor.SetProfileRule(Config, new ProfileRule("", "Default")));
        Assert.Throws<InvalidOperationException>(() => ConfigEditor.SetProfileRule(Config, new ProfileRule("x", "Missing")));
    }

    [Fact]
    public void RenameAndDeleteKeepRulesConsistent()
    {
        var renamed = ConfigEditor.RenameProfile(Config, "Discord", "Chat");
        Assert.Equal("Chat", renamed.AutoProfile.Rules[0].Profile);
        Assert.Equal("Chat", renamed.AutoProfile.FallbackProfile);

        var deleted = ConfigEditor.DeleteProfile(Config, "Discord");
        Assert.Empty(deleted.AutoProfile.Rules);
        Assert.Null(deleted.AutoProfile.FallbackProfile);
        Assert.Empty(ConfigValidator.Validate(deleted));
    }

    [Fact]
    public void ValidatorReportsBrokenRules()
    {
        var config = Config with
        {
            AutoProfile = new AutoProfileSettings
            {
                Rules = [new ProfileRule("a", "Missing"), new ProfileRule("b", "Default"), new ProfileRule("B", "Default")],
                FallbackProfile = "Nope"
            }
        };

        var issues = ConfigValidator.Validate(config);
        Assert.Equal(3, issues.Count);
    }
}
