using SpikeDce.Canonical;
using SpikeDce.Dce;

namespace SpikeDce.Tests;

public static class CanonicalFixture
{
    public static DespatchAdvice Create()
    {
        var cfg  = IssuerConfig.FromEnv();
        var emit = CompanyDirectory.Lookup(cfg.Cnpj14, cfg.XNomeOverride);     // real issuer by CNPJ
        var (destParty, items, vDc, serie, nDc, cDc6, dhEmi) = DceMockData.Generate(); // Bogus dest + lines

        static CanonAddress A(Address a) => new(
            a.XLgr, a.Nro, a.XCpl, a.XBairro, a.CMun, a.XMun, a.UF, a.CEP,
            a.CPais, a.XPais, a.Fone, a.Email);

        var supplier = new CanonParty(new TaxId("CNPJ", emit.Cnpj14), emit.XNome, A(emit.Ender));
        var customer = new CanonParty(new TaxId(destParty.Cnpj14 is not null ? "CNPJ" : "CPF",
            destParty.Cnpj14 ?? destParty.Cpf11!), destParty.XNome, A(destParty.Ender));

        var lines = items.Select(it => new DespatchLine(
            it.XProd, it.Ncm, it.QCom, it.VUnCom, it.VProd)).ToList();

        var shipment = new Shipment(new Carrier(new TaxId("CNPJ", emit.Cnpj14), emit.XNome),
            TransportModeCode: "2", DeclaredValueAmount: vDc);

        var dfe = new DfeExtensions(
            EmissionType: "2", Series: serie, Number: nDc, DocumentCode: cDc6,
            SiteAuthorizer: "0", Environment: "2", IssuedAt: dhEmi);

        return new DespatchAdvice(supplier, customer, shipment, lines, Note: null, dfe);
    }
}
