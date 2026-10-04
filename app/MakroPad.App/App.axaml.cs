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
        var args = new AppArguments(desktop.Args ?? []);

        config = new ConfigService(args.ConfigDirectory ?? ConfigService.DefaultDirectory);
        config.Load();
        ApplyTheme(config.Config.Settings.Theme);

        var simulator = args.Simulator ? new SimulatedDevice() : null;
        var serial = args.Simulator ? null : new SerialDeviceConnector { PreferredPort = config.State.LastPort };
        IDeviceConnector connector = simulator is not null ? simulator : serial!;

        var profiles = new ProfileManager();
        var actions = new ActionRunner(WindowsActions.CreateAll().Concat(new ProfileActions(profiles).Create()));
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
        recorder.Trace += diagnostics.Info;
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
            config.SaveState(config.State with { ActiveProfile = profile.Name });
            Dispatcher.UIThread.Post(RebuildProfileMenu);
        };
        runtime.StatusChanged += status =>
        {
            if (status == ConnectionStatus.Connected && serial is not null && runtime.Endpoint is { } port && port != config.State.LastPort)
            {
                config.SaveState(config.State with { LastPort = port });
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
        desktop.ShutdownRequested += (_, _) => Shutdown();

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
    /// Development aid: selects key 5, starts recording through its inspector field like the
    /// "Aufnehmen" button, sends Win+E, then writes the trace and the saved binding to a file.
    /// </summary>
    private async Task RecorderSelfTestAsync(RecorderAdapter recorder, string resultPath, IClassicDesktopStyleApplicationLifetime desktop)
    {
        var lines = new List<string>();
        recorder.Trace += line => { lock (lines) { lines.Add(line); } };
        await Task.Delay(1500);
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var key = shell!.Pad.Keys.First(k => k.Id == 5);
            shell.Pad.SelectCommand.Execute(key);
            var inspector = (BindingInspectorViewModel)shell.Pad.Inspector!;
            var field = inspector.Parameters.OfType<ShortcutParameterViewModel>().First();
            field.IsRecording = true;
            lines.Add($"recording={field.IsRecording} text={field.RecordText}");
            _ = Task.Run(async () =>
            {
                await Task.Delay(400);
                SelfTestKeys.WinE();
            });
            await Task.Delay(1500);
            lines.Add($"after: recording={field.IsRecording} value={field.Value} error={field.RecordError}");
            await Task.Delay(800);
            var saved = config!.Config.FindProfile(runtime!.Profiles.Active.Name)!.Bindings.First(b => b.Input == "button:5");
            lines.Add($"saved={saved.GetParameter("keys")} status={inspector.Status}");
        });
        lock (lines)
        {
            File.WriteAllLines(resultPath, lines);
        }

        exitRequested = true;
        desktop.Shutdown();
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
        exit.Click += (_, _) =>
        {
            exitRequested = true;
            desktop.Shutdown();
        };

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

    /// <summary>Development aid: <c>--screenshot file.png [--page diagnose|einstellungen] [--select button:5]</c>.</summary>
    private async Task TakeScreenshotAsync(AppArguments args, string path, IClassicDesktopStyleApplicationLifetime desktop)
    {
        await Task.Delay(TimeSpan.FromSeconds(2.5));
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (shell is null || window is null)
            {
                return;
            }

            shell.Page = args.Page switch
            {
                "diagnose" => AppPage.Diagnostics,
                "einstellungen" => AppPage.Settings,
                _ => AppPage.Pad
            };
            if (args.Select is { } select)
            {
                PadElementViewModel? element = select == "display"
                    ? shell.Pad.Display
                    : (PadElementViewModel?)shell.Pad.Keys.FirstOrDefault(k => k.Input == select)
                      ?? shell.Pad.Faders.FirstOrDefault(f => f.Input == select);
                if (element is not null)
                {
                    shell.Pad.SelectCommand.Execute(element);
                }
            }

            await Task.Delay(600);
            var size = new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height);
            using var bitmap = new RenderTargetBitmap(size);
            bitmap.Render(window);
            bitmap.Save(path);
            exitRequested = true;
            desktop.Shutdown();
        });
    }

    private void Shutdown()
    {
        exitRequested = true;
        trayIcon?.Dispose();
        foreground?.Dispose();
        media?.Dispose();
        config?.Dispose();
        runtime?.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private static string AppVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

    private static class SelfTestKeys
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern void keybd_event(byte vk, byte scan, uint flags, nint extra);

        public static void WinE()
        {
            keybd_event(0x5B, 0, 1, 0);
            keybd_event(0x45, 0, 0, 0);
            keybd_event(0x45, 0, 2, 0);
            keybd_event(0x5B, 0, 3, 0);
        }
    }

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
