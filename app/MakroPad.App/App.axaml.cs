using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using MakroPad.App.Services;
using MakroPad.App.ViewModels;
using MakroPad.Core.Actions;
using MakroPad.Core.Configuration;
using MakroPad.Core.Device;
using MakroPad.Core.Display;
using MakroPad.Core.Runtime;
using MakroPad.Device;
using MakroPad.Windows;

namespace MakroPad.App;

public partial class App : Application, IFilePicker
{
    private ConfigService? config;
    private MakroPadRuntime? runtime;
    private ShellViewModel? shell;
    private MainWindow? window;
    private TrayIcon? trayIcon;
    private ForegroundWatcher? foreground;
    private MediaSessionWatcher? media;
    private NativeMenuItem? profileMenu;
    private bool exitRequested;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Compose(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void Compose(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var allArgs = new AppArguments(desktop.Args ?? []);
        config = LoadConfig(allArgs);

        // Test options (simulator, screenshots, self-tests) only work in debug mode.
        var debug = config.Config.Settings.DebugMode;
        var args = debug ? allArgs : allArgs.WithoutDeveloperOptions();
        ApplyTheme(config.Config.Settings.Theme);

        IDeviceConnector connector = args.Simulator
            ? new SimulatedDevice()
            : new SerialDeviceConnector { PreferredPort = config.State.LastPort };
        var simulator = connector as SimulatedDevice;
        var serial = connector as SerialDeviceConnector;

        var profiles = new ProfileManager();
        var actions = new ActionRunner(
            WindowsActions.CreateAll()
                .Concat(new ProfileActions(profiles).Create())
                .Append(new HttpRequestAction(Http)),
            TimeProvider.System);
        runtime = new MakroPadRuntime(connector, actions, profiles, TimeProvider.System, AppVersion);
        profiles.Load(config.Config.Profiles, config.State.ActiveProfile);
        runtime.Settings = config.Config.Settings;
        ApplyAutostart(config.Config.Settings);

        // The OLED preview receives exactly the lines the device receives.
        var oled = new OledMirror(TimeProvider.System);
        runtime.LineLogged += (direction, line) =>
        {
            if (direction == LineDirection.ToDevice)
            {
                oled.Apply(line);
            }
        };
        runtime.StatusChanged += status => oled.SetHostConnected(status == ConnectionStatus.Connected);

        var session = new ConfigSession(config, runtime);
        Func<IReadOnlyList<string>> audioSessions = AppVolumeAction.ListSessionProcesses;
        var recorder = new RecorderAdapter();
        var diagnostics = new DiagnosticsViewModel(runtime, session, simulator, audioSessions);
        recorder.Trace += line =>
        {
            if (config.Config.Settings.DebugMode)
            {
                diagnostics.Info(line);
            }
        };
        var pad = new PadViewModel(session, runtime, oled, this, recorder, audioSessions);
        // Context: foreground program for automatic profiles, media sessions for the idle screen.
        foreground = new ForegroundWatcher();
        var switcher = new AutoProfileSwitcher(profiles, [Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "MakroPad.App")]);
        switcher.Apply(config.Config.AutoProfile);
        foreground.Changed += switcher.OnForegroundChanged;
        switcher.Switched += (process, profile) => diagnostics.Info($"{process} im Vordergrund: Profil {profile} aktiviert");
        session.Changed += _ => switcher.Apply(config.Config.AutoProfile);
        diagnostics.Attach(foreground);
        _ = AttachMediaAsync(runtime, diagnostics);

        var settings = new SettingsViewModel(session, new AutoProfileViewModel(session, foreground));
        settings.ThemeChanged += ApplyTheme;
        shell = new ShellViewModel(runtime, pad, diagnostics, settings);

        profiles.ActiveChanged += (profile, _) =>
        {
            if (config.SaveState(config.State with { ActiveProfile = profile.Name }) is { } error)
            {
                diagnostics.Warning(error);
            }
            Dispatcher.UIThread.Post(RebuildProfileMenu);
        };
        runtime.StatusChanged += status =>
        {
            if (status == ConnectionStatus.Connected && serial is not null && runtime.Endpoint is { } port && port != config.State.LastPort)
            {
                if (config.SaveState(config.State with { LastPort = port }) is { } error)
                {
                    diagnostics.Warning(error);
                }
            }
        };
        session.Changed += _ =>
        {
            ApplyAutostart(config.Config.Settings);
            ApplyTheme(config.Config.Settings.Theme);
            RebuildProfileMenu();
        };
        config.StartWatching();

        // Tray app: closing the window only hides it; "Beenden" in the tray menu exits.
        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        window = new MainWindow { DataContext = shell };
        window.Closing += (_, e) =>
        {
            if (!exitRequested)
            {
                e.Cancel = true;
                window.Hide();
            }
        };
        trayIcon = CreateTrayIcon(desktop);
        // Windows ending the session: hold it until the device and hooks are released.
        desktop.ShutdownRequested += (_, e) =>
        {
            if (!exitRequested)
            {
                e.Cancel = true;
                _ = ExitAsync(desktop);
            }
        };

        if (!args.Tray && !config.Config.Settings.StartInTray || args.Screenshot is not null)
        {
            ShowWindow();
        }

        runtime.Start();

        if (args.Screenshot is { } screenshotPath)
        {
            _ = TakeScreenshotAsync(args, screenshotPath, desktop);
        }

        if (args.RecorderSelfTest is { } resultPath)
        {
            _ = RecorderSelfTestAsync(recorder, resultPath, desktop);
        }
    }

    /// <summary>
    /// <c>--config-dir</c> is a developer option: it is only honoured when the config found there
    /// has debug mode on; otherwise the normal config folder is used.
    /// </summary>
    private static ConfigService LoadConfig(AppArguments args)
    {
        if (args.ConfigDirectory is { } directory)
        {
            var custom = new ConfigService(directory);
            custom.Load();
            if (custom.Config.Settings.DebugMode)
            {
                return custom;
            }

            custom.Dispose();
        }

        var service = new ConfigService(ConfigService.DefaultDirectory);
        service.Load();
        return service;
    }

    private async Task AttachMediaAsync(MakroPadRuntime target, DiagnosticsViewModel diagnostics)
    {
        media = await MediaSessionWatcher.StartAsync();
        target.Attach(media);
        diagnostics.Attach(media);
    }

    public async Task<string?> PickProgramAsync()
    {
        if (window?.StorageProvider is not { } storage)
        {
            return null;
        }

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Programm oder Datei wählen",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Programme") { Patterns = ["*.exe", "*.lnk", "*.bat", "*.cmd"] },
                FilePickerFileTypes.All
            ]
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    private void ApplyTheme(string theme)
    {
        RequestedThemeVariant = theme switch
        {
            "light" => ThemeVariant.Light,
            "dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }

    private static void ApplyAutostart(AppSettings settings)
    {
        if (Environment.ProcessPath is { } exe)
        {
            Autostart.Apply(settings.StartWithWindows, exe);
        }
    }

    private TrayIcon CreateTrayIcon(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var open = new NativeMenuItem("Öffnen");
        open.Click += (_, _) => ShowWindow();

        profileMenu = new NativeMenuItem("Profil") { Menu = new NativeMenu() };

        var exit = new NativeMenuItem("Beenden");
        exit.Click += (_, _) => _ = ExitAsync(desktop);

        var icon = new TrayIcon
        {
            Icon = new WindowIcon(Avalonia.Platform.AssetLoader.Open(new Uri("avares://MakroPad.App/Assets/makropad.ico"))),
            ToolTipText = "MakroPad",
            Menu = [open, profileMenu, new NativeMenuItemSeparator(), exit],
            IsVisible = true
        };
        icon.Clicked += (_, _) => ShowWindow();
        RebuildProfileMenu();
        return icon;
    }

    private void RebuildProfileMenu()
    {
        if (profileMenu?.Menu is not { } menu || runtime is null)
        {
            return;
        }

        menu.Items.Clear();
        foreach (var profile in runtime.Profiles.Profiles)
        {
            var name = profile.Name;
            var item = new NativeMenuItem(name)
            {
                ToggleType = MenuItemToggleType.Radio,
                IsChecked = string.Equals(name, runtime.Profiles.Active.Name, StringComparison.OrdinalIgnoreCase)
            };
            item.Click += (_, _) => runtime.Profiles.Set(name);
            menu.Items.Add(item);
        }
    }

    private void ShowWindow()
    {
        if (window is null)
        {
            return;
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: null } desktop)
        {
            desktop.MainWindow = window;
        }

        window.Show();
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
    }

    /// <summary>Releases the device, hooks and tray icon, then quits.</summary>
    private async Task ExitAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (exitRequested)
        {
            return;
        }

        exitRequested = true;
        try
        {
            trayIcon?.Dispose();
            foreground?.Dispose();
            media?.Dispose();
            config?.Dispose();
            if (runtime is not null)
            {
                // A device read that ignores cancellation must not keep the app alive.
                await runtime.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
            }
        }
        catch (TimeoutException)
        {
        }
        finally
        {
            desktop.Shutdown();
        }
    }

    private static readonly HttpClient Http = new() { Timeout = HttpRequestAction.Timeout };

    private static string AppVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

    private sealed class RecorderAdapter : IShortcutRecorder
    {
        private readonly ShortcutRecorder recorder = new();

        public event Action<string>? Trace
        {
            add => recorder.Trace += value;
            remove => recorder.Trace -= value;
        }

        public Task<string?> RecordAsync() => recorder.RecordAsync();

        public void Cancel() => recorder.Cancel();
    }

    private sealed class AppArguments(string[] args)
    {
        private static readonly string[] DeveloperOptions = ["--simulator", "--screenshot", "--page", "--select", "--selftest-recorder", "--config-dir"];

        /// <summary>Keeps only the options a normal user needs (currently <c>--tray</c>).</summary>
        public AppArguments WithoutDeveloperOptions()
        {
            var kept = new List<string>();
            for (var index = 0; index < args.Length; index++)
            {
                if (DeveloperOptions.Contains(args[index], StringComparer.OrdinalIgnoreCase))
                {
                    // Options with a value also drop the value.
                    if (!string.Equals(args[index], "--simulator", StringComparison.OrdinalIgnoreCase))
                    {
                        index++;
                    }

                    continue;
                }

                kept.Add(args[index]);
            }

            return new AppArguments([.. kept]);
        }

        public bool Simulator => Has("--simulator");

        public bool Tray => Has("--tray");

        public string? Screenshot => Value("--screenshot");

        public string? Page => Value("--page");

        public string? Select => Value("--select");

        public string? RecorderSelfTest => Value("--selftest-recorder");

        /// <summary>Alternative config folder, e.g. for screenshots with sample data.</summary>
        public string? ConfigDirectory => Value("--config-dir");

        private bool Has(string name) => args.Contains(name, StringComparer.OrdinalIgnoreCase);

        private string? Value(string name)
        {
            var index = Array.FindIndex(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
