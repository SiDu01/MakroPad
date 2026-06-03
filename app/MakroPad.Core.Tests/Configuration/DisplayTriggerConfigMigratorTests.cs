using MakroPad.Core.Configuration;

namespace MakroPad.Core.Tests.Configuration;

public sealed class DisplayTriggerConfigMigratorTests
{
    [Fact]
    public void MigrateDefaultsAddsActionResultAndMediaChangedDefaults()
    {
        var config = CreateConfig(actionResultTimeoutMs: 4321);

        var result = DisplayTriggerConfigMigrator.MigrateDefaults(config);

        Assert.True(result.AppSettings.DisplayTriggerDefaultsInitialized);
        Assert.True(result.AppSettings.MediaTriggerDefaultsInitialized);

        var actionTriggers = result.AppSettings.DisplayTriggers
            .Where(trigger => trigger.EventType == "actionResult")
            .ToList();
        Assert.Equal(2, actionTriggers.Count);

        var successTrigger = Assert.Single(actionTriggers, trigger => trigger.ActionStatus == "ok");
        Assert.True(successTrigger.Enabled);
        Assert.Equal("*", successTrigger.Input);
        Assert.Equal(4321, successTrigger.DurationMs);
        Assert.Equal("Action\n{action.message}", successTrigger.Template);

        var failedTrigger = Assert.Single(actionTriggers, trigger => trigger.ActionStatus == "failed");
        Assert.True(failedTrigger.Enabled);
        Assert.Equal("*", failedTrigger.Input);
        Assert.Equal(4321, failedTrigger.DurationMs);
        Assert.Equal("Action failed\n{action.message}", failedTrigger.Template);

        var mediaTrigger = Assert.Single(
            result.AppSettings.DisplayTriggers,
            trigger => trigger.EventType == "mediaChanged");
        Assert.True(mediaTrigger.Enabled);
        Assert.Equal("*", mediaTrigger.Input);
        Assert.Equal(0, mediaTrigger.DurationMs);
        Assert.Equal("Media\n{media.artist}\n{media.title}", mediaTrigger.Template);
    }

    [Fact]
    public void MigrateDefaultsPreservesExistingTriggersAndAppendsDefaultsInOrder()
    {
        var existingTrigger = new DisplayTriggerConfig
        {
            EventType = "input",
            Input = "button:7",
            Template = "Existing"
        };
        var config = CreateConfig(triggers: [existingTrigger]);

        var result = DisplayTriggerConfigMigrator.MigrateDefaults(config);

        Assert.Equal(4, result.AppSettings.DisplayTriggers.Count);
        Assert.Same(existingTrigger, result.AppSettings.DisplayTriggers[0]);
        Assert.Equal("actionResult", result.AppSettings.DisplayTriggers[1].EventType);
        Assert.Equal("ok", result.AppSettings.DisplayTriggers[1].ActionStatus);
        Assert.Equal("actionResult", result.AppSettings.DisplayTriggers[2].EventType);
        Assert.Equal("failed", result.AppSettings.DisplayTriggers[2].ActionStatus);
        Assert.Equal("mediaChanged", result.AppSettings.DisplayTriggers[3].EventType);
    }

    [Theory]
    [InlineData("actionResult")]
    [InlineData("ActionResult")]
    public void MigrateDefaultsDoesNotAddActionDefaultsWhenAnyNormalizedActionTriggerExists(
        string eventType)
    {
        var existingTrigger = new DisplayTriggerConfig
        {
            Enabled = false,
            EventType = eventType,
            ActionStatus = "any",
            Template = "Custom action"
        };
        var config = CreateConfig(triggers: [existingTrigger]);

        var result = DisplayTriggerConfigMigrator.MigrateDefaults(config);

        Assert.True(result.AppSettings.DisplayTriggerDefaultsInitialized);
        Assert.Same(existingTrigger, Assert.Single(
            result.AppSettings.DisplayTriggers,
            trigger => trigger.EventType == eventType));
        Assert.DoesNotContain(
            result.AppSettings.DisplayTriggers,
            trigger => trigger.ActionStatus is "ok" or "failed");
    }

    [Theory]
    [InlineData("mediaChanged")]
    [InlineData("MediaChanged")]
    public void MigrateDefaultsDoesNotAddMediaDefaultWhenAnyNormalizedMediaTriggerExists(
        string eventType)
    {
        var existingTrigger = new DisplayTriggerConfig
        {
            Enabled = false,
            EventType = eventType,
            Template = "Custom media"
        };
        var config = CreateConfig(triggers: [existingTrigger]);

        var result = DisplayTriggerConfigMigrator.MigrateDefaults(config);

        Assert.True(result.AppSettings.MediaTriggerDefaultsInitialized);
        Assert.Same(existingTrigger, Assert.Single(
            result.AppSettings.DisplayTriggers,
            trigger => trigger.EventType == eventType));
        Assert.DoesNotContain(
            result.AppSettings.DisplayTriggers,
            trigger => trigger.Template == "Media\n{media.artist}\n{media.title}");
    }

    [Fact]
    public void MigrateDefaultsRespectsActionInitializationFlag()
    {
        var config = CreateConfig(actionInitialized: true);

        var result = DisplayTriggerConfigMigrator.MigrateDefaults(config);

        Assert.DoesNotContain(
            result.AppSettings.DisplayTriggers,
            trigger => trigger.EventType == "actionResult");
        Assert.Contains(
            result.AppSettings.DisplayTriggers,
            trigger => trigger.EventType == "mediaChanged");
    }

    [Fact]
    public void MigrateDefaultsRespectsMediaInitializationFlag()
    {
        var config = CreateConfig(mediaInitialized: true);

        var result = DisplayTriggerConfigMigrator.MigrateDefaults(config);

        Assert.Contains(
            result.AppSettings.DisplayTriggers,
            trigger => trigger.EventType == "actionResult");
        Assert.DoesNotContain(
            result.AppSettings.DisplayTriggers,
            trigger => trigger.EventType == "mediaChanged");
    }

    [Fact]
    public void MigrateDefaultsReturnsSameConfigWhenBothFlagsAreInitialized()
    {
        var config = CreateConfig(actionInitialized: true, mediaInitialized: true);

        var result = DisplayTriggerConfigMigrator.MigrateDefaults(config);

        Assert.Same(config, result);
    }

    [Fact]
    public void MigrateDefaultsSetsMissingFlagsEvenWhenDefaultsAlreadyExist()
    {
        var actionTrigger = new DisplayTriggerConfig { EventType = "actionResult", Template = "Action" };
        var mediaTrigger = new DisplayTriggerConfig { EventType = "mediaChanged", Template = "Media" };
        var config = CreateConfig(triggers: [actionTrigger, mediaTrigger]);

        var result = DisplayTriggerConfigMigrator.MigrateDefaults(config);

        Assert.NotSame(config, result);
        Assert.True(result.AppSettings.DisplayTriggerDefaultsInitialized);
        Assert.True(result.AppSettings.MediaTriggerDefaultsInitialized);
        Assert.Equal(2, result.AppSettings.DisplayTriggers.Count);
        Assert.Same(actionTrigger, result.AppSettings.DisplayTriggers[0]);
        Assert.Same(mediaTrigger, result.AppSettings.DisplayTriggers[1]);
    }

    [Fact]
    public void MigrateDefaultsDoesNotMutateSourceConfigOrTriggerList()
    {
        var sourceTriggers = new List<DisplayTriggerConfig>
        {
            new() { EventType = "input", Input = "slider:1", Template = "Volume" }
        };
        var config = CreateConfig(triggers: sourceTriggers);

        var result = DisplayTriggerConfigMigrator.MigrateDefaults(config);

        Assert.False(config.AppSettings.DisplayTriggerDefaultsInitialized);
        Assert.False(config.AppSettings.MediaTriggerDefaultsInitialized);
        Assert.Same(sourceTriggers, config.AppSettings.DisplayTriggers);
        Assert.Single(sourceTriggers);
        Assert.NotSame(sourceTriggers, result.AppSettings.DisplayTriggers);
    }

    [Fact]
    public void MigrateDefaultsThrowsForNullConfig()
    {
        Assert.Throws<ArgumentNullException>(
            () => DisplayTriggerConfigMigrator.MigrateDefaults(null!));
    }

    private static MakroPadConfig CreateConfig(
        bool actionInitialized = false,
        bool mediaInitialized = false,
        int actionResultTimeoutMs = 3000,
        List<DisplayTriggerConfig>? triggers = null)
    {
        return new MakroPadConfig
        {
            Hardware = new HardwareConfig
            {
                Display = new DisplayHardwareConfig
                {
                    ActionResultTimeoutMs = actionResultTimeoutMs
                }
            },
            AppSettings = new AppSettingsConfig
            {
                DisplayTriggerDefaultsInitialized = actionInitialized,
                MediaTriggerDefaultsInitialized = mediaInitialized,
                DisplayTriggers = triggers ?? []
            }
        };
    }
}
