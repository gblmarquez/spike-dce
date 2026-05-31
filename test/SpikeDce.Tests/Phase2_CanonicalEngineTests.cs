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
}
