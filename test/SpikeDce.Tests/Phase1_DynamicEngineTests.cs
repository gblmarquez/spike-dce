using SpikeDce.Dce;
using SpikeDce.Schema;
using SpikeDce.Signing;
using Xunit;
using Xunit.Abstractions;

namespace SpikeDce.Tests;

public class Phase1_DynamicEngineTests
{
    private readonly ITestOutputHelper _out;
    public Phase1_DynamicEngineTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void SchemaModel_resolves_DCe_root_element()
    {
        var m = SchemaModel.Load(TestEnv.DceXsdDir);
        Assert.NotNull(m.GlobalElement("DCe", TestEnv.DceNs));
    }

    // H2: the dynamic dict→schema-ordered builder produces a DCe that (once signed, since ds:Signature is
    // mandatory in TDCe) validates against the same XSD + WSDL bar as the Phase-0 hand-built doc — zero per-DCe codegen.
    [Fact]
    public void EngineBuilt_dce_signed_validates_xsd_and_wsdl()
    {
        var model = SchemaModel.Load(TestEnv.DceXsdDir);
        var (data, chave) = HandBuiltDictFixture.Create();
        var xml = new SoapEnvelopeBuilder(model).BuildDocument("DCe", TestEnv.DceNs, data);
        _out.WriteLine(xml);
        Assert.Contains(IssuerConfig.FromEnv().Cnpj14, xml); // emitter CNPJ present

        var signed = DceSigner.SignEnveloped(xml, "DCe" + chave, CertificateLoader.LoadFromEnv(TestEnv.PfxPath));
        Assert.Empty(new DceXsdValidator(TestEnv.DceXsdDir).Validate(signed));
        var envelope = $"<dceDadosMsg xmlns=\"{TestEnv.WsdlNsAutoriz}\">{signed}</dceDadosMsg>";
        Assert.Empty(new DceWsdlValidator(TestEnv.AutorizWsdl, TestEnv.DceXsdDir).ValidateEnvelope(envelope));
    }
}
