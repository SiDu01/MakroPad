using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using MakroPad.App.ViewModels;
using MakroPad.Core.Device;

namespace MakroPad.App;

/// <summary>Developer aids, reachable only with debug mode on (see <see cref="AppArguments.WithoutDeveloperOptions"/>).</summary>
public partial class App
{
    /// <summary>
    /// Selects key 5, starts recording through its inspector field like the "Aufnehmen" button,
    /// sends Win+E, then writes the trace and the saved binding to a file.
    /// </summary>
    private async Task RecorderSelfTestAsync(RecorderAdapter recorder, string resultPath, IClassicDesktopStyleApplicationLifetime desktop)
    {
        var lines = new List<string>();
        recorder.Trace += line => { lock (lines) { lines.Add(line); } };
        await Task.Delay(1500);
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (shell is null || config is null || runtime is null)
            {
                return;
            }

            var key = shell.Pad.Keys.First(k => k.Id == 5);
            shell.Pad.SelectCommand.Execute(key);
            if (shell.Pad.Inspector is not KeyInspectorViewModel { Current: var inspector })
            {
                lines.Add("no key inspector");
                return;
            }

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
            var saved = config.Config.FindProfile(runtime.Profiles.Active.Name)?.Bindings.FirstOrDefault(b => b.Input == "button:5");
            lines.Add($"saved={saved?.GetParameter("keys")} status={inspector.Status}");
        });
        lock (lines)
        {
            File.WriteAllLines(resultPath, lines);
        }

        await ExitAsync(desktop);
    }

    /// <summary><c>--screenshot file.png [--page diagnose|einstellungen] [--select button:5]</c>.</summary>
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
                // button:9:long selects key 9 and its long-press tab.
                var parts = select.Split(':');
                var input = parts.Length > 2 ? $"{parts[0]}:{parts[1]}" : select;
                PadElementViewModel? element = input == "display"
                    ? shell.Pad.Display
                    : (PadElementViewModel?)shell.Pad.Keys.FirstOrDefault(k => k.Input == input)
                      ?? shell.Pad.Faders.FirstOrDefault(f => f.Input == input);
                if (element is not null)
                {
                    shell.Pad.SelectCommand.Execute(element);
                }

                if (parts.Length > 2 && shell.Pad.Inspector is KeyInspectorViewModel keyInspector)
                {
                    var gesture = parts[2] == "double" ? Gesture.DoublePress : Gesture.LongPress;
                    keyInspector.SelectTabCommand.Execute(keyInspector.Tabs.First(t => t.Gesture == gesture));
                }
            }

            await Task.Delay(600);
            var size = new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height);
            using var bitmap = new RenderTargetBitmap(size);
            bitmap.Render(window);
            bitmap.Save(path);
        });
        await ExitAsync(desktop);
    }

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
}
