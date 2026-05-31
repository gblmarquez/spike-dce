using SpikeDce.Dce;

namespace SpikeDce.Tests;

public static class DceDataFixture
{
    public static DceData Create()
    {
        var cfg  = IssuerConfig.FromEnv();
        var emit = CompanyDirectory.Lookup(cfg.Cnpj14, cfg.XNomeOverride);

        // cUF/UF follow the REAL emit company UF (issuer data by CNPJ), unless explicitly overridden by env.
        var uf  = cfg.UF  ?? emit.Ender.UF;
        var cuf = cfg.CUF ?? UfCodes.ToCode(uf);
        var issuer = new ResolvedIssuer(cfg.Cnpj14, cuf, uf);

        var (dest, items, vDc, serie, nDc, cDc6, dhEmi) = DceMockData.Generate();

        // Homologação (tpAmb=2) requires the destinatário name to be exactly this string (SEFAZ rule, cStat 598).
        const string tpAmb = "2";
        if (tpAmb == "2")
            dest = dest with { XNome = "DCE EMITIDA EM AMBIENTE DE HOMOLOGACAO" };

        return new DceData(issuer, emit, dest, items, vDc,
            ModTrans: "2", CnpjTransp: emit.Cnpj14, DhEmi: dhEmi,
            Serie: serie, NDc: nDc, CDc6: cDc6, TpAmb: tpAmb);
    }
}
