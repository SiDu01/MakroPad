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
        var pad = new PadViewModel(session, runtime, oled, this, audioSessions);
        var settings = new SettingsViewModel(session);
        settings.ThemeChanged += ApplyTheme;
        shell = new ShellViewModel(runtime, pad, new DiagnosticsViewModel(runtime, session, simulator, audioSessions), settings);

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
        config?.Dispose();
        runtime?.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private static string AppVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

    private sealed class AppArguments(string[] args)
    {
        public bool Simulator => Has("--simulator");

        public bool Tray => Has("--tray");

        public string? Screenshot => Value("--screenshot");

        public string? Page => Value("--page");

        public string? Select => Value("--select");

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
