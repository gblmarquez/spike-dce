using System.Net;

namespace SpikeDce.Tests;

// Captures the exact bytes posted to the transport (for H5 byte-preservation), echoes them back as the response.
public sealed class FakeEchoHandler : HttpMessageHandler
{
    public string? CapturedBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
    {
        CapturedBody = req.Content is null ? null : await req.Content.ReadAsStringAsync(ct);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(CapturedBody ?? "") };
    }
}
