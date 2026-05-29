using System.Diagnostics;

namespace MakroPad.Actions;

internal sealed class SystemLauncher : ISystemLauncher
{
    public Task<ActionExecutionResult> OpenUrlAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return Task.FromResult(ActionExecutionResult.Failed("URL must be an absolute http or https URL"));
        }

        return StartProcessAsync(uri.AbsoluteUri, "URL opened", cancellationToken);
    }

    public Task<ActionExecutionResult> OpenAppAsync(
        string target,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(target))
        {
            return Task.FromResult(ActionExecutionResult.Failed("App target is required"));
        }

        return StartProcessAsync(target.Trim(), "App opened", cancellationToken);
    }

    private static Task<ActionExecutionResult> StartProcessAsync(
        string target,
        string successMessage,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            Process.Start(new ProcessStartInfo(target)
            {
                UseShellExecute = true
            });

            return Task.FromResult(ActionExecutionResult.Ok(successMessage));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ActionExecutionResult.Failed(ex.Message));
        }
    }
}
