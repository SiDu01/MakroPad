using Avalonia.Threading;
using MakroPad.Core.Configuration;
using MakroPad.Core.Runtime;

namespace MakroPad.App.Services;

/// <summary>
/// The one place that changes the configuration while the app runs: edits from the UI and
/// reloads after external edits both end up in the runtime and raise <see cref="Changed"/> on the UI thread.
/// </summary>
public sealed class ConfigSession
{
    private readonly ConfigService service;
    private readonly MakroPadRuntime runtime;

    public ConfigSession(ConfigService service, MakroPadRuntime runtime)
    {
        this.service = service;
        this.runtime = runtime;
        service.ConfigChanged += () => Dispatcher.UIThread.Post(() => ApplyToRuntime(external: true));
    }

    public MakroPadConfig Config => service.Config;

    public ConfigService Service => service;

    /// <summary>Raised on the UI thread after the config changed; the flag is true for edits made outside the app.</summary>
    public event Action<bool>? Changed;

    /// <summary>Applies an edit, saves it and returns null, or returns the reason it was rejected.</summary>
    public string? Update(Func<MakroPadConfig, MakroPadConfig> edit)
    {
        try
        {
            service.Save(edit(service.Config));
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }
        catch (IOException ex)
        {
            return $"Konfiguration konnte nicht gespeichert werden: {ex.Message}";
        }

        ApplyToRuntime(external: false);
        return null;
    }

    public void ApplyToRuntime(bool external)
    {
        if (service.LoadError is null)
        {
            runtime.Profiles.Load(service.Config.Profiles);
            runtime.Settings = service.Config.Settings;
        }

        Changed?.Invoke(external);
    }
}
