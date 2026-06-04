using System;
using System.Threading;
using System.Threading.Tasks;

using MakroPad.Core.Configuration;

namespace MakroPad.App.Services;

public sealed class MakroPadConfigService
{
    private readonly MakroPadConfigStore configStore;
    private readonly SemaphoreSlim accessLock = new(1, 1);

    public MakroPadConfigService(MakroPadConfigStore configStore)
    {
        this.configStore = configStore;
    }

    public string ConfigPath => configStore.ConfigPath;

    public async Task<ConfigLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        await accessLock.WaitAsync(cancellationToken);
        try
        {
            var loadedConfig = await configStore.LoadOrCreateDefaultAsync(cancellationToken);
            var migratedConfig = DisplayTriggerConfigMigrator.MigrateDefaults(loadedConfig);
            var wasMigrated = !ReferenceEquals(loadedConfig, migratedConfig);
            if (wasMigrated)
            {
                await configStore.SaveAsync(migratedConfig, cancellationToken);
            }

            return new ConfigLoadResult(migratedConfig, wasMigrated);
        }
        finally
        {
            accessLock.Release();
        }
    }

    public async Task SaveAsync(MakroPadConfig config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        await accessLock.WaitAsync(cancellationToken);
        try
        {
            await configStore.SaveAsync(config, cancellationToken);
        }
        finally
        {
            accessLock.Release();
        }
    }
}

public sealed record ConfigLoadResult(MakroPadConfig Config, bool WasMigrated);
