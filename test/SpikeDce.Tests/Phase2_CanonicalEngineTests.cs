using SpikeDce.Canonical;
using SpikeDce.Mapping;
using Xunit;
using Xunit.Abstractions;

namespace SpikeDce.Tests;

public class Phase2_CanonicalEngineTests
{
    private readonly ITestOutputHelper _out;
    public Phase2_CanonicalEngineTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void Canonical_fixture_has_supplier_customer_and_lines()
    {
        var d = CanonicalFixture.Create();
        Assert.Equal("CNPJ", d.DespatchSupplierParty.TaxId.Scheme);
        Assert.Equal(14, d.DespatchSupplierParty.TaxId.Value.Length);
        Assert.NotEmpty(d.DespatchLines);
        Assert.Equal("2", d.Dfe.Environment);
    }

    [Fact]
    public void Validator_accepts_valid_canonical()
        => Assert.Empty(CanonicalValidator.Validate(CanonicalFixture.Create()));

    [Fact]
    public void Validator_rejects_bad_taxid_zero_lines_and_environment()
    {
        var d = CanonicalFixture.Create();
        var bad = d with
        {
            DespatchSupplierParty = d.DespatchSupplierParty with { TaxId = new TaxId("CNPJ", "123") },
            DespatchLines = new List<DespatchLine>(),
            Dfe = d.Dfe with { Environment = "9" },
        };
        var errors = CanonicalValidator.Validate(bad);
        Assert.Contains(errors, e => e.Contains("taxId"));
        Assert.Contains(errors, e => e.Contains("despatchLines"));
        Assert.Contains(errors, e => e.Contains("environment"));
    }

    [Fact]
    public void Engine_applies_const_copy_transform_and_derive()
    {
        var canonical = System.Text.Json.Nodes.JsonNode.Parse("""
            { "party": { "uf": "DF", "name": "ACME" }, "amount": 100.0 }
            """)!;
        var spec = MappingSpec.Parse("""
            {
              "derive": [ { "name": "cuf", "fn": "ufToCode", "args": [ { "from": "party.uf" } ] } ],
              "rules": [
                { "target": "ide.mod", "const": "99" },
                { "target": "ide.cUF", "from": "$cuf" },
                { "target": "emit.xNome", "from": "party.name" },
                { "target": "total.vDC", "fn": "decimal", "args": [ { "from": "amount" }, { "const": 2 } ] }
              ]
            }
            """);
        var dict = new MappingEngine().Apply(canonical, spec);

        var ide = (Dictionary<string, object?>)dict["ide"]!;
        Assert.Equal("99", ide["mod"]);
        Assert.Equal("53", ide["cUF"]);
        Assert.Equal("ACME", ((Dictionary<string, object?>)dict["emit"]!)["xNome"]);
        Assert.Equal("100.00", ((Dictionary<string, object?>)dict["total"]!)["vDC"]);
    }

    [Fact]
    public void Engine_forEach_builds_indexed_list_and_when_includes_or_omits()
    {
        var canonical = System.Text.Json.Nodes.JsonNode.Parse("""
            {
              "lines": [ { "name": "A", "qty": 1.0 }, { "name": "B", "qty": 2.0 } ],
              "carrier": { "id": "47712795000124" },
              "env": "2"
            }
            """)!;
        var spec = MappingSpec.Parse("""
            {
              "rules": [
                { "forEach": "lines", "target": "det[]", "index": "@nItem",
                  "rules": [
                    { "target": "prod.xProd", "from": "name" },
                    { "target": "prod.qCom", "fn": "decimal", "args": [ { "from": "qty" }, { "const": 4 } ] }
                  ] },
                { "target": "transp.CNPJTransp", "from": "carrier.id", "when": "carrier != null" },
                { "target": "dest.xNome", "const": "HOMOLOG", "when": "env == \"2\"" },
                { "target": "dest.cpf", "from": "missing", "when": "absent != null" }
              ]
            }
            """);
        var dict = new MappingEngine().Apply(canonical, spec);

        var det = (List<object?>)dict["det"]!;
        Assert.Equal(2, det.Count);
        var first = (Dictionary<string, object?>)det[0]!;
        Assert.Equal("1", first["@nItem"]);
        Assert.Equal("A", ((Dictionary<string, object?>)first["prod"]!)["xProd"]);
        Assert.Equal("1.0000", ((Dictionary<string, object?>)first["prod"]!)["qCom"]);
        Assert.Equal("47712795000124", ((Dictionary<string, object?>)dict["transp"]!)["CNPJTransp"]);
        Assert.Equal("HOMOLOG", ((Dictionary<string, object?>)dict["dest"]!)["xNome"]);
        Assert.False(dict.ContainsKey("dest") && ((Dictionary<string, object?>)dict["dest"]!).ContainsKey("cpf")); // omitted
    }

    [Fact]
    public void Canonical_mapped_dce_signed_validates_xsd_and_wsdl()
    {
        var d = CanonicalFixture.Create();
        Assert.Empty(CanonicalValidator.Validate(d));

        var canonical = System.Text.Json.JsonSerializer.SerializeToNode(d, JsonOpts)!;
        var spec = MappingSpec.Load(Path.Combine(TestEnv.AssetsDir, "mapping", "dce_v1.00.map.json"));
        var dict = new MappingEngine().Apply(canonical, spec);

        var model = SpikeDce.Schema.SchemaModel.Load(TestEnv.DceXsdDir);
        var xml = new SpikeDce.Schema.SoapEnvelopeBuilder(model).BuildDocument("DCe", TestEnv.DceNs, dict);
        _out.WriteLine(xml);
        Assert.Contains(d.DespatchSupplierParty.TaxId.Value, xml);

        var id = (string)((Dictionary<string, object?>)dict["infDCe"]!)["@Id"]!;
        var chave = id[3..];
        var signed = SpikeDce.Signing.EnvelopedXmlSigner.SignEnveloped(xml, "DCe" + chave,
            SpikeDce.Signing.CertificateLoader.LoadFromEnv(TestEnv.PfxPath));
        Assert.Empty(new SpikeDce.Schema.XsdValidator(TestEnv.DceXsdDir).Validate(signed));
        var env = $"<dceDadosMsg xmlns=\"{TestEnv.WsdlNsAutoriz}\">{signed}</dceDadosMsg>";
        Assert.Empty(new SpikeDce.Schema.SoapEnvelopeXsdValidator(TestEnv.AutorizWsdl, TestEnv.DceXsdDir).ValidateEnvelope(env));
    }

    private static readonly System.Text.Json.JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    [Fact]
    public async Task Canonical_mapped_dce_issues_against_homologacao()
    {
        if (!TestEnv.SefazEnabled) { _out.WriteLine("SEFAZ disabled"); return; }
        var d = CanonicalFixture.Create();
        Assert.Empty(CanonicalValidator.Validate(d));

        var canonical = System.Text.Json.JsonSerializer.SerializeToNode(d, JsonOpts)!;
        var spec = MappingSpec.Load(Path.Combine(TestEnv.AssetsDir, "mapping", "dce_v1.00.map.json"));
        var dict = new MappingEngine().Apply(canonical, spec);

        var model = SpikeDce.Schema.SchemaModel.Load(TestEnv.DceXsdDir);
        var xml = new SpikeDce.Schema.SoapEnvelopeBuilder(model).BuildDocument("DCe", TestEnv.DceNs, dict);
        var id = (string)((Dictionary<string, object?>)dict["infDCe"]!)["@Id"]!;
        var chave = id[3..];
        var signed = SpikeDce.Signing.EnvelopedXmlSigner.SignEnveloped(xml, "DCe" + chave,
            SpikeDce.Signing.CertificateLoader.LoadFromEnv(TestEnv.PfxPath));

        using var client = new SpikeDce.Transport.SefazSoapClient(SpikeDce.Signing.CertificateLoader.LoadFromEnv(TestEnv.PfxPath));
        var (status, body) = await client.SendAsync(TestEnv.HomologAutorizUrl, TestEnv.ActionAutoriz, TestEnv.WsdlNsAutoriz, signed);
        var r = SpikeDce.Dce.SefazRetResult.Parse(body);
        _out.WriteLine($"cStat={r.CStat} xMotivo={r.XMotivo} nProt={r.Protocolo}");
        Assert.Equal(200, status);
        Assert.Contains(r.CStat, new[] { "100", "204" }); // H6: same authorized result as Phase 0/1
    }

    [Fact]
    public void Transforms_cover_uf_accesskey_decimal_datetime_qrcode_concat()
    {
        Assert.Equal("53", Transforms.Invoke("ufToCode", new object?[] { "DF" }));
        Assert.Equal("2605", Transforms.Invoke("aamm", new object?[] { DateTimeOffset.Parse("2026-05-16T21:55:14-03:00") }));

        // access key: 9 ordered inputs → 44-digit chave whose last digit is the módulo-11 cDV
        var chave = (string)Transforms.Invoke("accessKey", new object?[]
            { "53", "2605", "47712795000124", "0", "1", "1", "2", "0", "100000" })!;
        Assert.Equal(44, chave.Length);
        Assert.Equal(chave[^1].ToString(), Transforms.Invoke("lastChar", new object?[] { chave }));

        Assert.Equal("100.00", Transforms.Invoke("decimal", new object?[] { 100m, 2 }));
        Assert.Equal("1.0000", Transforms.Invoke("decimal", new object?[] { 1m, 4 }));
        Assert.Equal("2026-05-16T21:55:14-03:00",
            Transforms.Invoke("dateTimeOffset", new object?[] { DateTimeOffset.Parse("2026-05-16T21:55:14-03:00") }));

        var qr = (string)Transforms.Invoke("qrCode", new object?[] { chave, "2" })!;
        Assert.StartsWith("https://www.fazenda.pr.gov.br/dce/qrcode?chDCe=" + chave + "&tpAmb=2", qr);
        Assert.True(qr.Length >= 94);
        Assert.Equal("DCe" + chave, Transforms.Invoke("concat", new object?[] { "DCe", chave }));
    }
}
