using System.Net;
using System.Net.Http.Json;

namespace ContextR.Propagation.Signing.IntegrationTests.Infrastructure;

internal sealed class HeaderCaptureHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var headers = request.Headers
            .ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase);

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(headers)
        };

        return Task.FromResult(response);
    }
}
