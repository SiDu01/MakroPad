using System.Text.Json;

namespace MakroPad.Core.Configuration;

public sealed class MakroPadConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase
    };

    public MakroPadConfigStore(string configPath)
    {
        ConfigPath = configPath;
    }

    public string ConfigPath { get; }

    public async Task<MakroPadConfig> LoadOrCreateDefaultAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(ConfigPath))
        {
            var defaultConfig = MakroPadConfig.CreateDefault();
            await SaveAsync(defaultConfig, cancellationToken);
            return defaultConfig;
        }

        await using var stream = File.OpenRead(ConfigPath);
        var config = await JsonSerializer.DeserializeAsync<MakroPadConfig>(stream, JsonOptions, cancellationToken);
        return config ?? MakroPadConfig.CreateDefault();
    }

    public async Task SaveAsync(MakroPadConfig config, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(ConfigPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(ConfigPath);
        await JsonSerializer.SerializeAsync(stream, config, JsonOptions, cancellationToken);
    }
}
