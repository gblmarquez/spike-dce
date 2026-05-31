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
