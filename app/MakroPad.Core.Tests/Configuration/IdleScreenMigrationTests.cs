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
                IdleScreens = []
            }
        };

        var migrated = DisplayTriggerConfigMigrator.MigrateDefaults(config);

        Assert.Empty(migrated.AppSettings.IdleScreens);
    }
}
