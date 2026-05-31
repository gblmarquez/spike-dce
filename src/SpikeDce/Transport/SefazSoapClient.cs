using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;

namespace SpikeDce.Transport;

// Minimal SOAP 1.2 + mTLS sender for the DC-e web services (PR/SVD).
// Self-contained (no AzTech dependency) — sends the signed DCe as a STRING (byte-preserving),
// wrapped in <dceDadosMsg> inside a SOAP 1.2 envelope, content-type application/soap+xml; action="...".
public sealed class SefazSoapClient : IDisposable
{
    private const string Soap12Ns = "http://www.w3.org/2003/05/soap-envelope";
    private readonly HttpClient _http;

    public SefazSoapClient(X509Certificate2 clientCert, HttpMessageHandler? inner = null)
    {
        if (inner is null)
        {
            var h = new HttpClientHandler { ClientCertificateOptions = ClientCertificateOption.Manual };
            h.ClientCertificates.Add(clientCert);
            // Gov server chain may not be in the container trust store; the spike accepts it explicitly.
            h.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
            _http = new HttpClient(h);
        }
        else
        {
            _http = new HttpClient(inner);
        }
        _http.Timeout = TimeSpan.FromSeconds(90);
    }

    // payloadXml = inner DC-e/cons/evento XML (its own ns …/dce). wrapperNs = WSDL ns of the target service.
    // wrapperElement defaults to dceDadosMsg; soapHeader is injected verbatim inside <soap12:Header> if set.
    // Returns (httpStatus, responseBody).
    public async Task<(int status, string body)> SendAsync(
        string url, string action, string wrapperNs, string payloadXml,
        string wrapperElement = "dceDadosMsg", string? soapHeader = null, CancellationToken ct = default)
    {
        var header = string.IsNullOrEmpty(soapHeader) ? "" : $"<soap12:Header>{soapHeader}</soap12:Header>";
        var soap =
            $"<soap12:Envelope xmlns:soap12=\"{Soap12Ns}\">{header}<soap12:Body>" +
            $"<{wrapperElement} xmlns=\"{wrapperNs}\">{payloadXml}</{wrapperElement}>" +
            $"</soap12:Body></soap12:Envelope>";

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        var content = new StringContent(soap, new System.Text.UTF8Encoding(false));
        var ctype = new MediaTypeHeaderValue("application/soap+xml") { CharSet = "utf-8" };
        ctype.Parameters.Add(new NameValueHeaderValue("action", $"\"{action}\""));
        content.Headers.ContentType = ctype;
        req.Content = content;

        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        return ((int)resp.StatusCode, body);
    }

    public void Dispose() => _http.Dispose();
}
