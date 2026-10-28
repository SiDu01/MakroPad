using System.Text.Json;

namespace MakroPad.Core.Configuration;

/// <summary>
/// Owns <c>config.json</c> (user-authored, reloaded on change) and <c>state.json</c> (app-owned).
/// </summary>
public sealed class ConfigService : IDisposable
{
    private static readonly TimeSpan ReloadDebounce = TimeSpan.FromMilliseconds(300);

    private readonly JsonFileStore<MakroPadConfig> configStore;
    private readonly JsonFileStore<AppState> stateStore;
    private readonly object gate = new();
    private FileSystemWatcher? watcher;
    private Timer? reloadTimer;
    private string? lastWrittenJson;

    public ConfigService(string directory)
    {
        Directory = directory;
        configStore = new JsonFileStore<MakroPadConfig>(Path.Combine(directory, "config.json"), MakroPadConfig.CreateDefault);
        stateStore = new JsonFileStore<AppState>(Path.Combine(directory, "state.json"), () => new AppState());
    }

    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MakroPad");

    public string Directory { get; }

    public string ConfigPath => configStore.Path;

    public MakroPadConfig Config { get; private set; } = MakroPadConfig.CreateDefault();

    public AppState State { get; private set; } = new();

    /// <summary>Set when config.json could not be read; <see cref="Config"/> then holds defaults.</summary>
    public string? LoadError { get; private set; }

    public IReadOnlyList<ConfigIssue> Issues { get; private set; } = [];

    /// <summary>Informational messages from loading, e.g. that an old config was archived.</summary>
    public IReadOnlyList<string> Notices { get; private set; } = [];

    /// <summary>Raised after config.json was reloaded because it changed on disk (any thread).</summary>
    public event Action? ConfigChanged;

    public void Load()
    {
        var notices = new List<string>();
        string? archiveError = null;
        try
        {
            if (ArchiveLegacyConfig() is { } archived)
            {
                notices.Add($"Alte Konfiguration (v0.2) nach {Path.GetFileName(archived)} verschoben, neue Standard-Konfiguration angelegt");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            archiveError = $"Alte Konfiguration (v0.2) konnte nicht verschoben werden: {ex.Message}";
        }

        LoadConfig();
        if (archiveError is not null)
        {
            // Treated like a broken file, so the old config is neither read as new nor overwritten.
            lock (gate)
            {
                Config = MakroPadConfig.CreateDefault();
                LoadError = archiveError;
                Issues = [];
            }
        }

        State = stateStore.Load().Value;
        Notices = notices;
    }

    /// <summary>
    /// Saves a config edited in the app and makes it current. The file watcher ignores this write,
    /// so <see cref="ConfigChanged"/> is only raised for edits made outside the app.
    /// While config.json cannot be read, <see cref="Config"/> holds defaults; saving would replace the user's file with them.
    /// </summary>
    public void Save(MakroPadConfig config)
    {
        lock (gate)
        {
            if (LoadError is not null)
            {
                throw new InvalidOperationException("config.json enthält einen Fehler und wird nicht überschrieben. Repariere die Datei (Details unter Diagnose).");
            }

            configStore.Save(config);
            lastWrittenJson = SafeRead(ConfigPath);
            Config = config;
            LoadError = null;
            Issues = ConfigValidator.Validate(config);
        }
    }

    public void SaveState(AppState state)
    {
        lock (gate)
        {
            State = state;
            stateStore.Save(state);
        }
    }

    public void StartWatching()
    {
        if (watcher is not null)
        {
            return;
        }

        reloadTimer = new Timer(_ => ReloadFromDisk(), null, Timeout.Infinite, Timeout.Infinite);
        watcher = new FileSystemWatcher(Directory, Path.GetFileName(ConfigPath))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
        };
        watcher.Changed += (_, _) => ScheduleReload();
        watcher.Created += (_, _) => ScheduleReload();
        watcher.Renamed += (_, _) => ScheduleReload();
        watcher.EnableRaisingEvents = true;
    }

    public void Dispose()
    {
        watcher?.Dispose();
        reloadTimer?.Dispose();
    }

    private void ScheduleReload() => reloadTimer?.Change(ReloadDebounce, Timeout.InfiniteTimeSpan);

    private void ReloadFromDisk()
    {
        lock (gate)
        {
            if (lastWrittenJson is not null && SafeRead(ConfigPath) == lastWrittenJson)
            {
                return;
            }
        }

        LoadConfig();
        ConfigChanged?.Invoke();
    }

    private void LoadConfig()
    {
        lock (gate)
        {
            var result = configStore.Load();
            Config = result.Value;
            LoadError = result.Error;
            Issues = result.Error is null ? ConfigValidator.Validate(result.Value) : [];
        }
    }

    private static string? SafeRead(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>Configs from app 0.2 have no <c>schemaVersion</c>; they are kept as a backup file.</summary>
    private string? ArchiveLegacyConfig()
    {
        if (!File.Exists(ConfigPath))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(ConfigPath), new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || document.RootElement.EnumerateObject().Any(p => p.NameEquals("schemaVersion"))
                || !document.RootElement.TryGetProperty("version", out _))
            {
                return null;
            }
        }
        catch (JsonException)
        {
            return null;
        }

        var archive = Path.Combine(Directory, "config.legacy-v0.2.json");
        File.Move(ConfigPath, archive, overwrite: true);
        return archive;
    }
}
