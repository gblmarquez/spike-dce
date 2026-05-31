using SpikeDce.Tables;
using Xunit;
using Xunit.Abstractions;

namespace SpikeDce.Tests;

public class Phase3_CodeTableTests
{
    private readonly ITestOutputHelper _out;
    public Phase3_CodeTableTests(ITestOutputHelper output) => _out = output;

    private static CodeTableRegistry Reg() => CodeTableRegistry.Load(Path.Combine(TestEnv.AssetsDir, "tables"));
    private static readonly DateOnly Apr = new(2026, 5, 1);
    private static readonly DateOnly Jan = new(2026, 2, 1);

    [Fact]
    public void Validate_membership_via_generated_xsd()
    {
        var reg = Reg();
        Assert.True(reg.Validate("cClassTrib", Apr, "000001"));
        Assert.False(reg.Validate("cClassTrib", Apr, "ZZZZZZ"));
    }

    [Fact]
    public void Version_selected_by_document_date()
    {
        var reg = Reg();
        Assert.Equal(new DateOnly(2026, 4, 15), reg.Active("cClassTrib", Apr).EffectiveFrom);
        Assert.Equal(new DateOnly(2026, 1, 1),  reg.Active("cClassTrib", Jan).EffectiveFrom);
        Assert.True (reg.Validate("cClassTrib", Apr, "000001"));   // April-only code valid for April
        Assert.False(reg.Validate("cClassTrib", Jan, "000001"));   // ...but not for January
        Assert.True (reg.Validate("cClassTrib", Jan, "000006"));   // January version code
    }

    [Fact]
    public void Lookup_enriches_dependent_value()
    {
        var reg = Reg();
        Assert.False(string.IsNullOrEmpty(reg.Lookup("cCredPres", new DateOnly(2026,1,1), "1", "descricao")));
        Assert.False(string.IsNullOrEmpty(reg.Lookup("meiosPagamento", new DateOnly(2026,4,1), "01", "descricao")));
    }
}
