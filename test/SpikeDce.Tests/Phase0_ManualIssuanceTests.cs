using SpikeDce.Dce;
using SpikeDce.Schema;
using SpikeDce.Signing;
using SpikeDce.Transport;
using Xunit;
using Xunit.Abstractions;

namespace SpikeDce.Tests;

public class Phase0_ManualIssuanceTests
{
    private readonly ITestOutputHelper _out;
    public Phase0_ManualIssuanceTests(ITestOutputHelper output) => _out = output;

    // build → sign → return (signed DCe xml, chave). The schema (TDCe) requires ds:Signature, so we
    // validate the SIGNED document. Needs the pfx env (DCE_SPIKE_PFX_PATH/PASSWORD).
    private static (string signed, string chave) BuildSigned()
    {
        var (xml, chave) = HandBuiltDce.Build(DceDataFixture.Create());
        var cert = CertificateLoader.LoadFromEnv(TestEnv.PfxPath);
        var signed = DceSigner.SignEnveloped(xml, "DCe" + chave, cert);
        return (signed, chave);
    }

    [Fact]
    public void Cdv_Modulo11_Base2to9_is_a_single_digit()
    {
        var dv = AccessKey.Modulo11Dv("3525".PadRight(43, '0'));
        Assert.InRange(dv, 0, 9);
    }

    [Fact]
    public void HandBuilt_dce_validates_against_xsd()
    {
        var (signed, chave) = BuildSigned();
        _out.WriteLine("chave=" + chave);
        _out.WriteLine(signed);
        var errors = new DceXsdValidator(TestEnv.DceXsdDir).Validate(signed);
        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }

    [Fact]
    public void HandBuilt_dce_validates_inside_wsdl_dceDadosMsg_envelope()
    {
        var (signed, _) = BuildSigned();
        var envelope = $"<dceDadosMsg xmlns=\"{TestEnv.WsdlNsAutoriz}\">{signed}</dceDadosMsg>";
        var errors = new DceWsdlValidator(TestEnv.AutorizWsdl, TestEnv.DceXsdDir).ValidateEnvelope(envelope);
        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }

    [Fact]
    public void Wsdl_validator_actually_validates_inner_dce_strictly()
    {
        // Proves the <xs:any processContents="strict"> really validates the inner DCe (not lax/skip):
        // a bogus inner element must produce validation errors.
        var bad = $"<dceDadosMsg xmlns=\"{TestEnv.WsdlNsAutoriz}\">" +
                  $"<DCe xmlns=\"{TestEnv.DceNs}\"><infDCe Id=\"DCe1\" versao=\"1.00\"><bogus/></infDCe></DCe></dceDadosMsg>";
        var errors = new DceWsdlValidator(TestEnv.AutorizWsdl, TestEnv.DceXsdDir).ValidateEnvelope(bad);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Signed_dce_self_verifies_rsa_sha1()
    {
        var (signed, chave) = BuildSigned();
        var doc = new System.Xml.XmlDocument { PreserveWhitespace = true };
        doc.LoadXml(signed);
        var sig = (System.Xml.XmlElement)doc.GetElementsByTagName("Signature", "http://www.w3.org/2000/09/xmldsig#")[0]!;
        var sx = new System.Security.Cryptography.Xml.SignedXml(doc);
        sx.LoadXml(sig);
        Assert.True(sx.CheckSignature());
        Assert.Contains("xmldsig#rsa-sha1", signed);
        Assert.Contains("#DCe" + chave, signed);
    }

    [Fact]
    public async Task StatusServico_homolog_responds()
    {
        if (!TestEnv.SefazEnabled) { _out.WriteLine("SEFAZ disabled"); return; }
        var cert = CertificateLoader.LoadFromEnv(TestEnv.PfxPath);
        using var client = new SefazDceClient(cert);
        var cuf = DceDataFixture.Create().Issuer.CUF;
        var cons = $"<consStatServDCe xmlns=\"{TestEnv.DceNs}\" versao=\"1.00\"><tpAmb>2</tpAmb><cUF>{cuf}</cUF><xServ>STATUS</xServ></consStatServDCe>";
        var (status, body) = await client.SendAsync(TestEnv.HomologStatusUrl, TestEnv.ActionStatus, TestEnv.WsdlNsStatus, cons);
        _out.WriteLine($"HTTP {status}");
        _out.WriteLine(body);
        var r = DceResult.Parse(body);
        _out.WriteLine($"cStat={r.CStat} xMotivo={r.XMotivo}");
        Assert.Equal(200, status);
        Assert.False(string.IsNullOrWhiteSpace(r.CStat));
    }

    [Fact]
    public async Task HandBuilt_dce_issues_against_homologacao()
    {
        if (!TestEnv.SefazEnabled) { _out.WriteLine("SEFAZ disabled"); return; }
        var (signed, chave) = BuildSigned();
        _out.WriteLine("chave=" + chave);
        using var client = new SefazDceClient(CertificateLoader.LoadFromEnv(TestEnv.PfxPath));
        var (status, body) = await client.SendAsync(TestEnv.HomologAutorizUrl, TestEnv.ActionAutoriz, TestEnv.WsdlNsAutoriz, signed);
        _out.WriteLine($"HTTP {status}");
        _out.WriteLine(body);
        var r = DceResult.Parse(body);
        _out.WriteLine($"cStat={r.CStat} xMotivo={r.XMotivo} nProt={r.Protocolo}");
        Assert.Equal(200, status);
        // H0 GATE: 100 = Autorizado (fresh key), 204 = Duplicidade (key already authorized) — both prove
        // the request was well-formed + signature-valid + schema-valid and fully processed by SEFAZ.
        Assert.Contains(r.CStat, new[] { "100", "204" });
    }
}
