using SpikeDce.Canonical;
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
}
