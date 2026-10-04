using System.Reflection;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MakroPad.Core.Actions;
using MakroPad.Core.Device;
using MakroPad.Core.Runtime;
using MakroPad.Device;
using MakroPad.Windows;

namespace MakroPad.App;

public partial class App : Application
{
    private MakroPadRuntime? runtime;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // --simulator runs the whole app against a software device.
            var simulator = desktop.Args?.Contains("--simulator", StringComparer.OrdinalIgnoreCase) == true
                ? new SimulatedDevice()
                : null;
            IDeviceConnector connector = simulator is not null ? simulator : new SerialDeviceConnector();

            runtime = new MakroPadRuntime(
                connector,
                new ActionRunner(WindowsActions.CreateAll()),
                new InputRouter(DefaultBindings.All),
                TimeProvider.System,
                AppVersion);

            desktop.MainWindow = new MainWindow { DataContext = new MainViewModel(runtime, simulator) };
            desktop.ShutdownRequested += (_, _) => runtime.DisposeAsync().AsTask().GetAwaiter().GetResult();
            runtime.Start();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static string AppVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
}
