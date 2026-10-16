using System.ComponentModel;
using System.Diagnostics;
using MakroPad.Core.Actions;

namespace MakroPad.Windows;

/// <summary><c>keyboard.text</c>: types <c>text</c> into the focused window; <c>\n</c> in the config means Enter.</summary>
public sealed class KeyboardTextAction : IActionHandler
{
    public string ActionId => "keyboard.text";

    public Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var text = (context.Binding.GetParameter("text") ?? "").Replace("\\n", "\n", StringComparison.Ordinal);
        if (text.Length == 0)
        {
            return Task.FromResult(ActionResult.Failed("text fehlt"));
        }

        NativeInput.TypeText(text);
        var preview = text.Replace('\n', ' ');
        return Task.FromResult(ActionResult.Ok(preview.Length > 20 ? preview[..20] + "…" : preview));
    }
}

/// <summary>
/// <c>system.run</c>: runs a command line hidden through <c>cmd.exe</c>. It waits a few seconds so a
/// failing command can be reported; longer-running commands keep running and count as started.
/// </summary>
public sealed class RunCommandAction : IActionHandler
{
    public static readonly TimeSpan WaitForExit = TimeSpan.FromSeconds(5);

    public string ActionId => "system.run";

    public async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var command = context.Binding.GetParameter("command")?.Trim() ?? "";
        if (command.Length == 0)
        {
            return ActionResult.Failed("command fehlt");
        }

        var name = Describe(command);
        Process? process;
        try
        {
            // /s with the outer quotes keeps the quoting inside the command intact.
            process = Process.Start(new ProcessStartInfo("cmd.exe", $"/d /s /c \"{command}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return ActionResult.Failed($"{name}: {ex.Message}");
        }

        if (process is null)
        {
            return ActionResult.Failed($"{name}: konnte nicht gestartet werden");
        }

        using (process)
        {
            using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            wait.CancelAfter(WaitForExit);
            try
            {
                await process.WaitForExitAsync(wait.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return ActionResult.Ok($"{name} läuft");
            }

            return process.ExitCode == 0
                ? ActionResult.Ok(name)
                : ActionResult.Failed($"{name}: Exit-Code {process.ExitCode}");
        }
    }

    /// <summary>First word of the command without path, e.g. <c>backup</c> for <c>C:\x\backup.cmd -y</c>.</summary>
    public static string Describe(string command)
    {
        var first = command.StartsWith('"') && command.IndexOf('"', 1) is > 0 and var end
            ? command[1..end]
            : command.Split(' ', 2)[0];
        var name = Path.GetFileNameWithoutExtension(first);
        return name.Length > 0 ? name : first;
    }
}
