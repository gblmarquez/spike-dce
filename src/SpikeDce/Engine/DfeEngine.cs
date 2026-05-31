using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Serialization;
using SpikeDce.Dce;
using SpikeDce.Mapping;
using SpikeDce.Schema;
using SpikeDce.Signing;
using SpikeDce.Transport;

namespace SpikeDce.Engine;

public sealed record BuiltDocument(Binding Binding, string Chave, string SignedXml);

public sealed class DfeEngine
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly BindingRegistry _bindings;
    private readonly string _assetsDir;
    public DfeEngine(BindingRegistry bindings, string assetsDir) { _bindings = bindings; _assetsDir = assetsDir; }

    public BuiltDocument BuildSigned(FiscalEvent ev, X509Certificate2 cert)
    {
        var b = _bindings.Resolve(ev.Model, ev.Verb);
        var canonical = JsonSerializer.SerializeToNode(ev.Payload, ev.Payload.GetType(), JsonOpts)!;
        var spec = MappingSpec.Load(Path.Combine(_assetsDir, b.Map));
        var dict = new MappingEngine().Apply(canonical, spec);

        var model = SchemaModel.Load(Path.Combine(_assetsDir, b.XsdDir), b.RootXsd);
        var xml = new SoapEnvelopeBuilder(model).BuildDocument(b.RootElement, b.Ns, dict);

        var id = (string)GetByPath(dict, b.SignedIdPath)!;
        var signed = EnvelopedXmlSigner.SignEnveloped(xml, id, cert);
        var chave = id.StartsWith("DCe") ? id[3..] : id;
        return new BuiltDocument(b, chave, signed);
    }

    public async Task<SefazRetResult> Submit(FiscalEvent ev, EngineMode mode, X509Certificate2 cert, CancellationToken ct = default)
    {
        var built = BuildSigned(ev, cert);
        if (mode == EngineMode.Offline) return new SefazRetResult("(offline)", "", null, built.SignedXml);
        using var client = new SefazSoapClient(cert);
        var (_, body) = await client.SendAsync(built.Binding.ServiceUrl, built.Binding.SoapAction,
            built.Binding.WrapperNs, built.SignedXml, built.Binding.WrapperElement, built.Binding.SoapHeader, ct);
        return SefazRetResult.Parse(body);
    }

    private static object? GetByPath(Dictionary<string, object?> root, string path)
    {
        object? cur = root;
        foreach (var seg in path.Split('.'))
        {
            if (cur is not Dictionary<string, object?> m || !m.TryGetValue(seg, out cur))
                throw new InvalidOperationException($"signedIdPath '{path}' not found at '{seg}'");
        }
        return cur;
    }
}
