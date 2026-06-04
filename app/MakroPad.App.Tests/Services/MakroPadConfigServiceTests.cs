using MakroPad.App.Services;
using MakroPad.Core.Configuration;

namespace MakroPad.App.Tests.Services;

public sealed class MakroPadConfigServiceTests
{
    [Fact]
    public async Task LoadAsyncMigratesAndPersistsLegacyConfig()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"MakroPad.App.Tests.{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "config.json");

        try
        {
            var store = new MakroPadConfigStore(path);
            await store.SaveAsync(new MakroPadConfig
            {
                AppSettings = new AppSettingsConfig
                {
                    DisplayTriggers = []
                }
            });

            var service = new MakroPadConfigService(store);
            var result = await service.LoadAsync();
            var reloaded = await store.LoadOrCreateDefaultAsync();

            Assert.True(result.WasMigrated);
            Assert.True(result.Config.AppSettings.DisplayTriggerDefaultsInitialized);
            Assert.True(reloaded.AppSettings.MediaTriggerDefaultsInitialized);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
