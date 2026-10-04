using MakroPad.Core.Actions;
using MakroPad.Core.Configuration;

namespace MakroPad.Core.Tests.Configuration;

public sealed class ConfigEditorTests
{
    private static readonly MakroPadConfig Config = new()
    {
        Profiles =
        [
            new ProfileConfig
            {
                Name = "Default",
                Bindings =
                [
                    new Binding("button:1", "media.playPause"),
                    new Binding("button:2", "profile.set", new Dictionary<string, string> { ["name"] = "Gaming" })
                ]
            },
            new ProfileConfig { Name = "Gaming" }
        ]
    };

    [Fact]
    public void SetBindingReplacesOrAppends()
    {
        var replaced = ConfigEditor.SetBinding(Config, "default", new Binding("BUTTON:1", "media.stop"));
        var appended = ConfigEditor.SetBinding(Config, "Default", new Binding("button:9", "media.mute"));

        Assert.Equal("media.stop", replaced.Profiles[0].Bindings[0].Action);
        Assert.Equal(2, replaced.Profiles[0].Bindings.Count);
        Assert.Equal(3, appended.Profiles[0].Bindings.Count);
        Assert.Equal("media.playPause", Config.Profiles[0].Bindings[0].Action);
    }

    [Fact]
    public void RemoveBindingDropsOnlyThatInput()
    {
        var result = ConfigEditor.RemoveBinding(Config, "Default", "button:1");

        Assert.Equal("button:2", Assert.Single(result.Profiles[0].Bindings).Input);
    }

    [Fact]
    public void RenameUpdatesProfileSetTargets()
    {
        var result = ConfigEditor.RenameProfile(Config, "Gaming", "Spiele");

        Assert.Equal("Spiele", result.Profiles[1].Name);
        Assert.Equal("Spiele", result.Profiles[0].Bindings[1].GetParameter("name"));
        Assert.Empty(ConfigValidator.Validate(result));
    }

    [Fact]
    public void AddDuplicateAndDeleteProfiles()
    {
        var name = ConfigEditor.UniqueProfileName(Config, "Default");
        var duplicated = ConfigEditor.AddProfile(Config, name, Config.Profiles[0]);
        Assert.Equal("Default 2", name);
        Assert.Equal(2, duplicated.FindProfile("Default 2")!.Bindings.Count);

        var deleted = ConfigEditor.DeleteProfile(duplicated, "Gaming");
        Assert.Null(deleted.FindProfile("Gaming"));
    }

    [Fact]
    public void InvalidProfileEditsAreRejected()
    {
        Assert.Throws<InvalidOperationException>(() => ConfigEditor.AddProfile(Config, "gaming"));
        Assert.Throws<InvalidOperationException>(() => ConfigEditor.AddProfile(Config, "  "));
        Assert.Throws<InvalidOperationException>(() => ConfigEditor.RenameProfile(Config, "Default", "Gaming"));
        Assert.Throws<InvalidOperationException>(() => ConfigEditor.DeleteProfile(Config with { Profiles = [Config.Profiles[0]] }, "Default"));
        Assert.Throws<InvalidOperationException>(() => ConfigEditor.SetBinding(Config, "Missing", new Binding("button:1", "media.stop")));
    }

    [Fact]
    public void MoveProfileSwapsNeighbours()
    {
        var moved = ConfigEditor.MoveProfile(Config, "Gaming", -1);
        Assert.Equal(["Gaming", "Default"], moved.Profiles.Select(p => p.Name));
        Assert.Same(Config, ConfigEditor.MoveProfile(Config, "Default", -1));
    }

    [Fact]
    public void SaveDoesNotTriggerExternalReload()
    {
        var directory = Path.Combine(Path.GetTempPath(), "makropad-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var service = new ConfigService(directory);
            service.Load();
            var raised = 0;
            service.ConfigChanged += () => Interlocked.Increment(ref raised);
            service.StartWatching();

            service.Save(Config);
            Thread.Sleep(800);

            Assert.Equal(0, Volatile.Read(ref raised));
            Assert.Equal("Gaming", service.Config.Profiles[1].Name);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
