using MakroPad.Core.Actions;
using MakroPad.Core.Configuration;
using MakroPad.Core.Runtime;

namespace MakroPad.Core.Tests.Configuration;

public sealed class ConfigValidatorTests
{
    private static MakroPadConfig With(params Binding[] bindings) => new()
    {
        Profiles = [new ProfileConfig { Name = "Default", Bindings = bindings }]
    };

    [Fact]
    public void DefaultConfigIsValid()
    {
        Assert.Empty(ConfigValidator.Validate(MakroPadConfig.CreateDefault()));
    }

    [Theory]
    [InlineData("button:17", "media.playPause", "Unbekannte Eingabe")]
    [InlineData("knob:1", "media.playPause", "Unbekannte Eingabe")]
    [InlineData("button:1", "media.fly", "Unbekannte Aktion")]
    [InlineData("button:1", "system.volume", "passt nicht zu einer Taste")]
    [InlineData("slider:1", "media.playPause", "passt nicht zu einem Slider")]
    [InlineData("slider:1", "app.volume", "Parameter \"process\" fehlt")]
    [InlineData("button:1", "profile.set", "Parameter \"name\" fehlt")]
    public void BindingProblemsAreReported(string input, string action, string expected)
    {
        var issue = Assert.Single(ConfigValidator.Validate(With(new Binding(input, action))));
        Assert.Contains(expected, issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ParameterValuesAreChecked()
    {
        var issues = ConfigValidator.Validate(With(
            new Binding("button:1", "system.openUrl", new Dictionary<string, string> { ["url"] = "example.com" }),
            new Binding("button:2", "keyboard.shortcut", new Dictionary<string, string> { ["keys"] = "Ctrl+Hyper" }),
            new Binding("button:3", "profile.set", new Dictionary<string, string> { ["name"] = "Nope" }),
            new Binding("button:4", "media.stop", new Dictionary<string, string> { ["volume"] = "1" })));

        Assert.Collection(
            issues,
            i => Assert.Contains("http", i.Message, StringComparison.Ordinal),
            i => Assert.Contains("Hyper", i.Message, StringComparison.Ordinal),
            i => Assert.Contains("Nope", i.Message, StringComparison.Ordinal),
            i => Assert.Contains("nicht verwendet", i.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void DuplicateProfilesAndInputsAreReported()
    {
        var config = new MakroPadConfig
        {
            Profiles =
            [
                new ProfileConfig { Name = "A", Bindings = [new Binding("button:1", "media.stop"), new Binding("BUTTON:1", "media.mute")] },
                new ProfileConfig { Name = "a" }
            ]
        };

        var issues = ConfigValidator.Validate(config);

        Assert.Contains(issues, i => i.Message.Contains("mehrfach", StringComparison.Ordinal));
        Assert.Contains(issues, i => i.Message.Contains("doppelt", StringComparison.Ordinal));
    }

    [Fact]
    public void EveryCatalogActionIsDocumented()
    {
        var doc = File.ReadAllText(Path.Combine(RepoRoot(), "docs", "CONFIG.md"));
        Assert.All(ActionCatalog.All, action => Assert.Contains($"| `{action.Id}` |", doc, StringComparison.Ordinal));
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "docs")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("docs folder not found");
    }

    [Fact]
    public void EveryCatalogActionHasUniqueId()
    {
        Assert.Equal(ActionCatalog.All.Count, ActionCatalog.All.Select(a => a.Id.ToLowerInvariant()).Distinct().Count());
    }
}

public sealed class ShortcutParserTests
{
    [Fact]
    public void ModifiersComeFirstInAnyOrder()
    {
        Assert.True(ShortcutParser.TryParse("A+ctrl+Shift", out var keys, out _));
        Assert.Equal([ShortcutParser.Control, ShortcutParser.Shift, (ushort)'A'], keys);
    }

    [Theory]
    [InlineData("F5", 0x74)]
    [InlineData("F24", 0x87)]
    [InlineData("esc", 0x1B)]
    [InlineData("7", '7')]
    [InlineData("PageDown", 0x22)]
    public void SingleKeysParse(string text, int expected)
    {
        Assert.True(ShortcutParser.TryParse(text, out var keys, out _));
        Assert.Equal((ushort)expected, Assert.Single(keys));
    }

    [Fact]
    public void RecordedKeysFormatCanonicallyAndParseBack()
    {
        // VK_LWIN + VK_RSHIFT + 'E'
        var text = ShortcutParser.Format([0x5B, 0xA1], 'E');
        Assert.Equal("Shift+Win+E", text);
        Assert.True(ShortcutParser.TryParse(text!, out var keys, out _));
        Assert.Equal([ShortcutParser.Shift, ShortcutParser.Win, (ushort)'E'], keys);

        Assert.Equal("Ctrl+Alt+F13", ShortcutParser.Format([0xA2, 0xA5], 0x7C));
        Assert.Equal("7", ShortcutParser.Format([], 0x67));
        Assert.Null(ShortcutParser.Format([], 0xFF));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ctrl+")]
    [InlineData("Ctrl+Shift")]
    [InlineData("F25")]
    [InlineData("Ctrl+Hyper")]
    public void InvalidShortcutsFail(string text)
    {
        Assert.False(ShortcutParser.TryParse(text, out _, out var error));
        Assert.NotEmpty(error);
    }
}

public sealed class ConfigServiceTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "makropad-tests-" + Guid.NewGuid().ToString("N"));

    public ConfigServiceTests() => Directory.CreateDirectory(directory);

    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Fact]
    public void MissingConfigIsCreatedWithDefaults()
    {
        using var service = new ConfigService(directory);
        service.Load();

        Assert.True(File.Exists(service.ConfigPath));
        Assert.Null(service.LoadError);
        Assert.Equal("Default", Assert.Single(service.Config.Profiles).Name);
    }

    [Fact]
    public void ConfigRoundTripsWithCommentsAndParameters()
    {
        File.WriteAllText(Path.Combine(directory, "config.json"), """
            {
              // comments are allowed
              "schemaVersion": 1,
              "settings": { "startInTray": true, "feedback": { "volume": false } },
              "profiles": [
                { "name": "Musik", "bindings": [
                  { "input": "slider:2", "action": "app.volume", "parameters": { "process": "Spotify" } },
                ] }
              ]
            }
            """);
        using var service = new ConfigService(directory);
        service.Load();

        Assert.Null(service.LoadError);
        Assert.Empty(service.Issues);
        Assert.True(service.Config.Settings.StartInTray);
        Assert.False(service.Config.Settings.Feedback.Volume);
        Assert.True(service.Config.Settings.Feedback.Actions);
        Assert.Equal("Spotify", service.Config.Profiles[0].Bindings[0].GetParameter("process"));
    }

    [Fact]
    public void BrokenConfigIsKeptAndDefaultsAreUsed()
    {
        var path = Path.Combine(directory, "config.json");
        File.WriteAllText(path, "{ \"schemaVersion\": 1, \"profiles\": [ ");
        using var service = new ConfigService(directory);
        service.Load();

        Assert.NotNull(service.LoadError);
        Assert.Equal("{ \"schemaVersion\": 1, \"profiles\": [ ", File.ReadAllText(path));
        Assert.Single(service.Config.Profiles);
    }

    [Fact]
    public void LegacyConfigIsArchived()
    {
        File.WriteAllText(Path.Combine(directory, "config.json"), "{ \"version\": 1, \"activeProfile\": \"Default\" }");
        using var service = new ConfigService(directory);
        service.Load();

        Assert.True(File.Exists(Path.Combine(directory, "config.legacy-v0.2.json")));
        Assert.Single(service.Notices);
        Assert.Null(service.LoadError);
    }

    [Fact]
    public void StateIsStoredSeparately()
    {
        using (var service = new ConfigService(directory))
        {
            service.Load();
            service.SaveState(new AppState { ActiveProfile = "Gaming", LastPort = "COM6" });
        }

        using var reloaded = new ConfigService(directory);
        reloaded.Load();
        Assert.Equal("Gaming", reloaded.State.ActiveProfile);
        Assert.Equal("COM6", reloaded.State.LastPort);
    }

    [Fact]
    public async Task ExternalEditsAreReloaded()
    {
        using var service = new ConfigService(directory);
        service.Load();
        var reloaded = new TaskCompletionSource();
        service.ConfigChanged += () => reloaded.TrySetResult();
        service.StartWatching();

        File.WriteAllText(service.ConfigPath, """{ "schemaVersion": 1, "profiles": [ { "name": "Neu" } ] }""");

        await reloaded.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("Neu", Assert.Single(service.Config.Profiles).Name);
    }
}

public sealed class ProfileManagerTests
{
    private static readonly ProfileConfig[] Profiles =
    [
        new() { Name = "A" },
        new() { Name = "B" },
        new() { Name = "C" }
    ];

    [Fact]
    public void LoadPrefersRequestedThenKeepsCurrent()
    {
        var manager = new ProfileManager();
        manager.Load(Profiles, "b");
        Assert.Equal("B", manager.Active.Name);

        manager.Load(Profiles);
        Assert.Equal("B", manager.Active.Name);

        manager.Load([new ProfileConfig { Name = "X" }]);
        Assert.Equal("X", manager.Active.Name);
    }

    [Fact]
    public void NextWrapsAndSetReportsUserSwitch()
    {
        var manager = new ProfileManager();
        manager.Load(Profiles, "C");
        var switches = new List<(string, bool)>();
        manager.ActiveChanged += (profile, user) => switches.Add((profile.Name, user));

        manager.Next();
        Assert.True(manager.Set("b"));
        Assert.False(manager.Set("missing"));

        Assert.Equal([("A", true), ("B", true)], switches);
    }

    [Fact]
    public void EmptyProfileListFallsBackToDefault()
    {
        var manager = new ProfileManager();
        manager.Load([]);
        Assert.Equal("Default", manager.Active.Name);
    }
}
