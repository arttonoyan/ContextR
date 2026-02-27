using System.Net;
using System.Net.Http.Json;

namespace ContextR.Http.IntegrationTests.Infrastructure;

/// <summary>
/// A stub <see cref="HttpMessageHandler"/> that captures the outgoing request headers
/// and returns them as a JSON dictionary in the response body.
/// Each invocation is independent, making this handler safe for concurrent use.
/// </summary>
internal sealed class HeaderCaptureHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string>();
        foreach (var header in request.Headers)
            headers[header.Key] = string.Join(",", header.Value);

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(headers)
        });
    }
}
