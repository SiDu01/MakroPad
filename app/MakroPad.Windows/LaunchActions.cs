using System.ComponentModel;
using System.Diagnostics;
using MakroPad.Core.Actions;
using MakroPad.Core.Configuration;

namespace MakroPad.Windows;

/// <summary><c>system.openUrl</c>: opens an http(s) address in the default browser.</summary>
public sealed class OpenUrlAction : IActionHandler
{
    public string ActionId => "system.openUrl";

    public Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var url = context.Binding.GetParameter("url") ?? "";
        if (!ConfigValidator.IsHttpUrl(url))
        {
            return Task.FromResult(ActionResult.Failed("url muss mit http:// oder https:// beginnen"));
        }

        return Task.FromResult(Shell.Start(new ProcessStartInfo(url) { UseShellExecute = true }, new Uri(url).Host));
    }
}

/// <summary><c>system.openApp</c>: starts a program or opens a file via the Windows shell.</summary>
public sealed class OpenAppAction : IActionHandler
{
    public string ActionId => "system.openApp";

    public Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var path = context.Binding.GetParameter("path")?.Trim().Trim('"');
        if (string.IsNullOrEmpty(path))
        {
            return Task.FromResult(ActionResult.Failed("path fehlt"));
        }

        var startInfo = new ProcessStartInfo(Environment.ExpandEnvironmentVariables(path))
        {
            UseShellExecute = true,
            Arguments = context.Binding.GetParameter("args") ?? ""
        };
        return Task.FromResult(Shell.Start(startInfo, Path.GetFileNameWithoutExtension(path)));
    }
}

internal static class Shell
{
    public static ActionResult Start(ProcessStartInfo startInfo, string label)
    {
        try
        {
            using var _ = Process.Start(startInfo);
            return ActionResult.Ok(label);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            return ActionResult.Failed($"{label}: {ex.Message}");
        }
    }
}
