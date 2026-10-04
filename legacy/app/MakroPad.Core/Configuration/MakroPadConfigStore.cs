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

    /// <summary>
    /// Reads the existing config synchronously without creating a default. Returns null when the file
    /// is missing or cannot be parsed. Used at startup to decide whether to show the window.
    /// </summary>
    public MakroPadConfig? TryLoadExisting()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                return null;
            }

            using var stream = File.OpenRead(ConfigPath);
            return JsonSerializer.Deserialize<MakroPadConfig>(stream, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

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

        var tempPath = $"{ConfigPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, config, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(tempPath, ConfigPath, true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
