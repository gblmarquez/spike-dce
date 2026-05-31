using System.Security.Cryptography.Xml;
using System.Xml;
using SpikeDce.Dce;
using SpikeDce.Schema;
using SpikeDce.Signing;
using SpikeDce.Transport;
using Xunit;
using Xunit.Abstractions;

namespace SpikeDce.Tests;

public class Phase1_DynamicEngineTests
{
    private readonly ITestOutputHelper _out;
    public Phase1_DynamicEngineTests(ITestOutputHelper output) => _out = output;

    // engine path: dict → SchemaModel/SoapEnvelopeBuilder → sign. Shared by the H4/H5 tests.
    private static (string signed, string chave) BuildSignedEngine()
    {
        var model = SchemaModel.Load(TestEnv.DceXsdDir);
        var (data, chave) = HandBuiltDictFixture.Create();
        var xml = new SoapEnvelopeBuilder(model).BuildDocument("DCe", TestEnv.DceNs, data);
        var signed = EnvelopedXmlSigner.SignEnveloped(xml, "DCe" + chave, CertificateLoader.LoadFromEnv(TestEnv.PfxPath));
        return (signed, chave);
    }

    private static bool Verifies(string dceXml)
    {
        var doc = new XmlDocument { PreserveWhitespace = true };
        doc.LoadXml(dceXml);
        var sig = (XmlElement)doc.GetElementsByTagName("Signature", "http://www.w3.org/2000/09/xmldsig#")[0]!;
        var sx = new SignedXml(doc);
        sx.LoadXml(sig);
        return sx.CheckSignature();
    }

    [Fact]
    public void SchemaModel_resolves_DCe_root_element()
    {
        var m = SchemaModel.Load(TestEnv.DceXsdDir);
        Assert.NotNull(m.GlobalElement("DCe", TestEnv.DceNs));
    }

    // H2: the dynamic dict→schema-ordered builder produces a DCe that (once signed, since ds:Signature is
    // mandatory in TDCe) validates against the same XSD + WSDL bar as Phase 0 — zero per-DCe codegen.
    [Fact]
    public void EngineBuilt_dce_signed_validates_xsd_and_wsdl()
    {
        var model = SchemaModel.Load(TestEnv.DceXsdDir);
        var (data, chave) = HandBuiltDictFixture.Create();
        var xml = new SoapEnvelopeBuilder(model).BuildDocument("DCe", TestEnv.DceNs, data);
        _out.WriteLine(xml);
        Assert.Contains(IssuerConfig.FromEnv().Cnpj14, xml); // emitter CNPJ present

        var signed = EnvelopedXmlSigner.SignEnveloped(xml, "DCe" + chave, CertificateLoader.LoadFromEnv(TestEnv.PfxPath));
        Assert.Empty(new XsdValidator(TestEnv.DceXsdDir).Validate(signed));
        var envelope = $"<dceDadosMsg xmlns=\"{TestEnv.WsdlNsAutoriz}\">{signed}</dceDadosMsg>";
        Assert.Empty(new SoapEnvelopeXsdValidator(TestEnv.AutorizWsdl, TestEnv.DceXsdDir).ValidateEnvelope(envelope));
    }

    // H4 (reuse): the engine-built DCe enveloped-signs with the real cert and self-verifies.
    [Fact]
    public void EngineBuilt_dce_self_signs_and_verifies()
    {
        var (signed, chave) = BuildSignedEngine();
        Assert.True(Verifies(signed));
        Assert.Contains("xmldsig#rsa-sha1", signed);
        Assert.Contains("#DCe" + chave, signed);
    }

    // H5: the signed bytes survive the transport hand-off byte-for-byte and still verify after the round-trip.
    [Fact]
    public async Task EngineBuilt_signed_bytes_survive_transport_and_verify()
    {
        var (signed, _) = BuildSignedEngine();
        var echo = new FakeEchoHandler();
        using var client = new SefazSoapClient(CertificateLoader.LoadFromEnv(TestEnv.PfxPath), echo);
        await client.SendAsync("https://transport.test/x", TestEnv.ActionAutoriz, TestEnv.WsdlNsAutoriz, signed);

        Assert.NotNull(echo.CapturedBody);
        Assert.Contains(signed, echo.CapturedBody!); // signed DCe present verbatim (byte-preserving)

        var start = echo.CapturedBody!.IndexOf("<DCe", StringComparison.Ordinal);
        var end = echo.CapturedBody!.IndexOf("</DCe>", StringComparison.Ordinal) + "</DCe>".Length;
        var extracted = echo.CapturedBody!.Substring(start, end - start);
        Assert.True(Verifies(extracted)); // signature still verifies on the captured copy
    }

    // H5 (live): the engine-built DCe issues with the same authorized result as the Phase-0 hand-built one.
    [Fact]
    public async Task EngineBuilt_dce_issues_against_homologacao()
    {
        if (!TestEnv.SefazEnabled) { _out.WriteLine("SEFAZ disabled"); return; }
        var (signed, chave) = BuildSignedEngine();
        _out.WriteLine("chave=" + chave);
        using var client = new SefazSoapClient(CertificateLoader.LoadFromEnv(TestEnv.PfxPath));
        var (status, body) = await client.SendAsync(TestEnv.HomologAutorizUrl, TestEnv.ActionAutoriz, TestEnv.WsdlNsAutoriz, signed);
        var r = SefazRetResult.Parse(body);
        _out.WriteLine($"cStat={r.CStat} xMotivo={r.XMotivo} nProt={r.Protocolo}");
        Assert.Equal(200, status);
        Assert.Contains(r.CStat, new[] { "100", "204" }); // same authorized result as the Phase-0 hand-built doc
    }
}
