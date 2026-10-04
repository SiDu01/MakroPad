using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using MakroPad.Core.Actions;
using MakroPad.Core.Configuration;
using MakroPad.Core.Device;
using MakroPad.Core.Runtime;
using MakroPad.Device;
using MakroPad.Windows;

namespace MakroPad.App;

public partial class App : Application
{
    private ConfigService? config;
    private MakroPadRuntime? runtime;
    private MainViewModel? viewModel;
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
        var args = desktop.Args ?? [];
        var useSimulator = args.Contains("--simulator", StringComparer.OrdinalIgnoreCase);

        config = new ConfigService(ConfigService.DefaultDirectory);
        config.Load();

        var simulator = useSimulator ? new SimulatedDevice() : null;
        var serial = useSimulator ? null : new SerialDeviceConnector { PreferredPort = config.State.LastPort };
        IDeviceConnector connector = simulator is not null ? simulator : serial!;

        var profiles = new ProfileManager();
        var actions = new ActionRunner(WindowsActions.CreateAll().Concat(new ProfileActions(profiles).Create()));
        runtime = new MakroPadRuntime(connector, actions, profiles, TimeProvider.System, AppVersion);

        viewModel = new MainViewModel(runtime, simulator, config);
        viewModel.ReportConfig(config.Notices);

        profiles.Load(config.Config.Profiles, config.State.ActiveProfile);
        runtime.Settings = config.Config.Settings;
        ApplyAutostart(config.Config.Settings);

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
        config.ConfigChanged += () => Dispatcher.UIThread.Post(OnConfigChanged);
        config.StartWatching();

        // Tray app: closing the window only hides it; "Beenden" in the tray menu exits.
        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        window = new MainWindow { DataContext = viewModel };
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

        var startHidden = args.Contains("--tray", StringComparer.OrdinalIgnoreCase) || config.Config.Settings.StartInTray;
        if (!startHidden)
        {
            ShowWindow();
        }

        runtime.Start();
    }

    private void OnConfigChanged()
    {
        if (config is null || runtime is null || viewModel is null)
        {
            return;
        }

        if (config.LoadError is null)
        {
            runtime.Profiles.Load(config.Config.Profiles);
            runtime.Settings = config.Config.Settings;
            ApplyAutostart(config.Config.Settings);
        }

        viewModel.ReportConfig(["Konfiguration neu geladen"]);
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

        var editConfig = new NativeMenuItem("Konfiguration bearbeiten");
        editConfig.Click += (_, _) => viewModel?.OpenConfigCommand.Execute(null);

        var exit = new NativeMenuItem("Beenden");
        exit.Click += (_, _) =>
        {
            exitRequested = true;
            desktop.Shutdown();
        };

        var icon = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader("avares://MakroPad.App/Assets/makropad.ico")),
            ToolTipText = "MakroPad",
            Menu = [open, profileMenu, editConfig, new NativeMenuItemSeparator(), exit],
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

    private void Shutdown()
    {
        exitRequested = true;
        trayIcon?.Dispose();
        config?.Dispose();
        runtime?.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private static Stream AssetLoader(string uri) => Avalonia.Platform.AssetLoader.Open(new Uri(uri));

    private static string AppVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
}
