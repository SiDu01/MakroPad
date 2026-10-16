using System.Net.Http.Headers;
using System.Text;
using MakroPad.Core.Configuration;

namespace MakroPad.Core.Actions;

/// <summary><c>http.request</c>: sends a request and reports the status code.</summary>
public sealed class HttpRequestAction(HttpClient client) : IActionHandler
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public string ActionId => "http.request";

    public async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var url = context.Binding.GetParameter("url") ?? "";
        if (!ConfigValidator.IsHttpUrl(url))
        {
            return ActionResult.Failed("url muss mit http:// oder https:// beginnen");
        }

        var body = context.Binding.GetParameter("body");
        var methodName = context.Binding.GetParameter("method") is { Length: > 0 } given
            ? given.Trim().ToUpperInvariant()
            : string.IsNullOrEmpty(body) ? "GET" : "POST";
        if (!ConfigValidator.HttpMethods.Contains(methodName))
        {
            return ActionResult.Failed($"Unbekannte Methode {methodName}");
        }

        using var request = new HttpRequestMessage(new HttpMethod(methodName), url);
        if (!string.IsNullOrEmpty(body))
        {
            var contentType = context.Binding.GetParameter("contentType") is { Length: > 0 } type ? type.Trim() : "application/json";
            request.Content = new StringContent(body, Encoding.UTF8);
            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        var host = new Uri(url).Host;
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            var code = (int)response.StatusCode;
            return response.IsSuccessStatusCode
                ? ActionResult.Ok($"{host}: {code}")
                : ActionResult.Failed($"{host}: {code} {response.ReasonPhrase}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ActionResult.Failed($"{host} antwortet nicht");
        }
        catch (HttpRequestException ex)
        {
            return ActionResult.Failed($"{host}: {ex.Message}");
        }
    }
}
