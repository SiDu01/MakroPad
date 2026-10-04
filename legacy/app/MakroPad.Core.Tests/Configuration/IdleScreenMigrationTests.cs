using MakroPad.Core.Configuration;

namespace MakroPad.Core.Tests.Configuration;

public sealed class IdleScreenMigrationTests
{
    [Fact]
    public void MigrateCreatesStandardIdleScreenFromDisplayDefaults()
    {
        var config = MakroPadConfig.CreateDefault();

        var migrated = DisplayTriggerConfigMigrator.MigrateDefaults(config);

        Assert.True(migrated.AppSettings.IdleScreensInitialized);
        Assert.Single(migrated.AppSettings.IdleScreens);
        var idle = migrated.AppSettings.IdleScreens[0];
        Assert.Equal("Standard", idle.Name);
        Assert.Equal("default", idle.Activation);
        Assert.Contains("{profile}", idle.Template);
    }

    [Fact]
    public void MigrateDoesNotReAddIdleScreensOnceInitialized()
    {
        var config = MakroPadConfig.CreateDefault() with
        {
            AppSettings = MakroPadConfig.CreateDefault().AppSettings with
            {
                IdleScreensInitialized = true,
                IdleScreensConditionsInitialized = true,
                IdleScreens = []
            }
        };

        var migrated = DisplayTriggerConfigMigrator.MigrateDefaults(config);

        Assert.Empty(migrated.AppSettings.IdleScreens);
    }

    [Fact]
    public void MigrateConvertsLegacyProcessScreenIntoCondition()
    {
        var baseConfig = MakroPadConfig.CreateDefault();
        var config = baseConfig with
        {
            AppSettings = baseConfig.AppSettings with
            {
                IdleScreensInitialized = true,
                IdleScreensConditionsInitialized = false,
                IdleScreens =
                [
                    new IdleScreenConfig { Name = "Default", Activation = "default" },
                    new IdleScreenConfig { Name = "Spotify", Activation = "process", ProcessName = "Spotify" }
                ]
            }
        };

        var migrated = DisplayTriggerConfigMigrator.MigrateDefaults(config);

        Assert.True(migrated.AppSettings.IdleScreensConditionsInitialized);

        var standard = migrated.AppSettings.IdleScreens[0];
        Assert.Empty(standard.Conditions);
        Assert.Equal(0, standard.Priority);

        var spotify = migrated.AppSettings.IdleScreens[1];
        var condition = Assert.Single(spotify.Conditions);
        Assert.Equal("processRunning", condition.Type);
        Assert.Equal("Spotify", condition.Target);
        Assert.Equal(10, spotify.Priority);
    }

    [Fact]
    public void MigrateLeavesExistingConditionsUntouched()
    {
        var baseConfig = MakroPadConfig.CreateDefault();
        var config = baseConfig with
        {
            AppSettings = baseConfig.AppSettings with
            {
                IdleScreensInitialized = true,
                IdleScreensConditionsInitialized = false,
                IdleScreens =
                [
                    new IdleScreenConfig
                    {
                        Name = "Media",
                        Activation = "process",
                        ProcessName = "Spotify",
                        Conditions = [new IdleScreenCondition { Type = "mediaActive", Target = "Spotify" }]
                    }
                ]
            }
        };

        var migrated = DisplayTriggerConfigMigrator.MigrateDefaults(config);

        var screen = migrated.AppSettings.IdleScreens[0];
        var condition = Assert.Single(screen.Conditions);
        Assert.Equal("mediaActive", condition.Type);
    }
}
