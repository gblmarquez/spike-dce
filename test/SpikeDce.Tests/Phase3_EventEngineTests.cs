using SpikeDce.Engine;
using SpikeDce.Schema;
using Xunit;
using Xunit.Abstractions;

namespace SpikeDce.Tests;

public class Phase3_EventEngineTests
{
    private readonly ITestOutputHelper _out;
    public Phase3_EventEngineTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void SchemaModel_loads_evento_root()
    {
        var dir = Path.Combine(TestEnv.AssetsDir, "xsd", "evento");
        var m = SchemaModel.Load(dir, "eventoDCe_v1.00.xsd");
        Assert.NotNull(m.GlobalElement("eventoDCe", TestEnv.DceNs));
    }

    [Fact]
    public void Transforms_padLeft_and_now()
    {
        Assert.Equal("001", SpikeDce.Mapping.Transforms.Invoke("padLeft", new object?[] { "1", 3, "0" }));
        var now = (string)SpikeDce.Mapping.Transforms.Invoke("now", System.Array.Empty<object?>())!;
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}[-+]\d{2}:\d{2}$", now);
    }

    [Fact]
    public void Cancel_map_builds_eventoDCe_with_signed_id()
    {
        var req = new SpikeDce.Canonical.CancelRequest(
            AccessKey: "53260547712795000124990000005713151208020940",
            Protocol: "3532600000023909", Justification: "Cancelamento de teste do spike SpikeDce",
            Sequence: "1", Context: new SpikeDce.Canonical.EventContext("2", "41",
                new SpikeDce.Canonical.TaxId("CNPJ", "47712795000124")));
        var canonical = System.Text.Json.JsonSerializer.SerializeToNode(req, JsonOpts)!;
        var spec = SpikeDce.Mapping.MappingSpec.Load(Path.Combine(TestEnv.AssetsDir, "mapping", "evento_cancel_v1.00.map.json"));
        var dict = new SpikeDce.Mapping.MappingEngine().Apply(canonical, spec);
        var inf = (Dictionary<string, object?>)dict["infEvento"]!;
        Assert.Equal("ID11011153260547712795000124990000005713151208020940001", inf["@Id"]);
        var det = (Dictionary<string,object?>)inf["detEvento"]!;
        var ev = (Dictionary<string,object?>)det["evCancDCe"]!;
        Assert.Equal("3532600000023909", ev["nProt"]);
        Assert.Equal("Cancelamento", ev["descEvento"]);
    }

    [Fact]
    public void BindingRegistry_resolves_dce_issue_and_cancel()
    {
        var reg = BindingRegistry.Load(Path.Combine(TestEnv.AssetsDir, "bindings"));
        Assert.Equal("DCe", reg.Resolve("dce", "issue").RootElement);
        Assert.Equal("eventoDCe", reg.Resolve("dce", "cancel").RootElement);
        Assert.Equal("infEvento.@Id", reg.Resolve("dce", "cancel").SignedIdPath);
    }

    [Fact]
    public void DfeEngine_offline_issue_and_cancel_validate()
    {
        var engine = new DfeEngine(BindingRegistry.Load(Path.Combine(TestEnv.AssetsDir, "bindings")), TestEnv.AssetsDir);
        var cert = SpikeDce.Signing.CertificateLoader.LoadFromEnv(TestEnv.PfxPath);

        var issue = engine.BuildSigned(FiscalEvent.Issue("dce", CanonicalFixture.Create()), cert);
        Assert.Empty(new SpikeDce.Schema.XsdValidator(TestEnv.DceXsdDir).Validate(issue.SignedXml));
        var issEnv = $"<dceDadosMsg xmlns=\"{issue.Binding.WrapperNs}\">{issue.SignedXml}</dceDadosMsg>";
        Assert.Empty(new SpikeDce.Schema.SoapEnvelopeXsdValidator(TestEnv.AutorizWsdl, TestEnv.DceXsdDir).ValidateEnvelope(issEnv));

        var req = new SpikeDce.Canonical.CancelRequest(
            "53260547712795000124990000005713151208020940", "3532600000023909",
            "Cancelamento de teste do spike SpikeDce", "1",
            new SpikeDce.Canonical.EventContext("2", "41", new SpikeDce.Canonical.TaxId("CNPJ", "47712795000124")));
        var cancel = engine.BuildSigned(FiscalEvent.Cancel("dce", req), cert);
        var evXsdDir = Path.Combine(TestEnv.AssetsDir, "xsd", "evento");
        Assert.Empty(new SpikeDce.Schema.XsdValidator(evXsdDir, "eventoDCe_v1.00.xsd").Validate(cancel.SignedXml));
        var evWsdl = Path.Combine(TestEnv.AssetsDir, "wsdl", "homologacao", "DCeRecepcaoEvento.wsdl");
        var evEnv = $"<{cancel.Binding.WrapperElement} xmlns=\"{cancel.Binding.WrapperNs}\">{cancel.SignedXml}</{cancel.Binding.WrapperElement}>";
        Assert.Empty(new SpikeDce.Schema.SoapEnvelopeXsdValidator(evWsdl, evXsdDir, "eventoDCe_v1.00.xsd").ValidateEnvelope(evEnv));
    }

    private static readonly System.Text.Json.JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };
}
