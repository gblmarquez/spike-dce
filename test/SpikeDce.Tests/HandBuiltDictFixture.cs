using System.Globalization;
using SpikeDce.Dce;

namespace SpikeDce.Tests;

// Nested Dictionary<string,object?> mirroring the SAME DceDataFixture used in Phase 0 (real emit by CNPJ + Bogus
// dest/items). This is the engine's input; SoapEnvelopeBuilder turns it into schema-ordered XML. No hardcoded issuer.
public static class HandBuiltDictFixture
{
    public static (Dictionary<string, object?> dce, string chave) Create()
    {
        var d = DceDataFixture.Create();
        var (_, chave) = HandBuiltDce.Build(d); // reuse the hand-built path to get the consistent chave + cDV
        var cDV = chave[^1].ToString();

        static string Dec(decimal v, int dec) => v.ToString("F" + dec, CultureInfo.InvariantCulture);

        static Dictionary<string, object?> Ender(Address a, bool dest)
        {
            var m = new Dictionary<string, object?> { ["xLgr"] = a.XLgr, ["nro"] = a.Nro };
            if (!string.IsNullOrWhiteSpace(a.XCpl)) m["xCpl"] = a.XCpl;
            m["xBairro"] = a.XBairro; m["cMun"] = a.CMun; m["xMun"] = a.XMun;
            m["UF"] = a.UF; m["CEP"] = a.CEP; m["cPais"] = a.CPais; m["xPais"] = a.XPais;
            if (!string.IsNullOrWhiteSpace(a.Fone)) m["fone"] = a.Fone;
            if (dest && !string.IsNullOrWhiteSpace(a.Email)) m["email"] = a.Email;
            return m;
        }

        var ide = new Dictionary<string, object?>
        {
            ["cUF"] = d.Issuer.CUF, ["cDC"] = d.CDc6, ["mod"] = "99", ["serie"] = d.Serie, ["nDC"] = d.NDc,
            ["dhEmi"] = d.DhEmi.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture),
            ["tpEmis"] = "1", ["tpEmit"] = "2", ["nSiteAutoriz"] = "0", ["cDV"] = cDV,
            ["tpAmb"] = d.TpAmb, ["verProc"] = "SpikeDce/1.0",
        };

        var emit = new Dictionary<string, object?>
        {
            ["CNPJ"] = d.Emit.Cnpj14, ["xNome"] = d.Emit.XNome, ["enderEmit"] = Ender(d.Emit.Ender, dest: false),
        };

        var dest = new Dictionary<string, object?> { ["xNome"] = d.Dest.XNome, ["enderDest"] = Ender(d.Dest.Ender, dest: true) };
        if (d.Dest.Cnpj14 is not null) dest["CNPJ"] = d.Dest.Cnpj14; else dest["CPF"] = d.Dest.Cpf11;

        var det = d.Items.Select((it, i) => (object?)new Dictionary<string, object?>
        {
            ["@nItem"] = (i + 1).ToString(),
            ["prod"] = new Dictionary<string, object?>
            {
                ["xProd"] = it.XProd, ["NCM"] = it.Ncm,
                ["qCom"] = Dec(it.QCom, 4), ["vUnCom"] = Dec(it.VUnCom, 2), ["vProd"] = Dec(it.VProd, 2),
            },
        }).ToList();

        var transp = new Dictionary<string, object?> { ["modTrans"] = d.ModTrans };
        if (!string.IsNullOrWhiteSpace(d.CnpjTransp)) transp["CNPJTransp"] = d.CnpjTransp;

        var infDce = new Dictionary<string, object?>
        {
            ["@Id"] = "DCe" + chave, ["@versao"] = "1.00",
            ["ide"] = ide, ["emit"] = emit, ["dest"] = dest, ["det"] = det,
            ["total"] = new Dictionary<string, object?> { ["vDC"] = Dec(d.VDc, 2) },
            ["transp"] = transp,
            ["infDec"] = new Dictionary<string, object?> { ["xObs1"] = HandBuiltDce.XObs1, ["xObs2"] = HandBuiltDce.XObs2 },
        };

        var dce = new Dictionary<string, object?>
        {
            ["infDCe"] = infDce,
            ["infSolicDCe"] = new Dictionary<string, object?> { ["xSolic"] = "Emissao propria de DC-e para transporte de bens." },
            ["infDCeSupl"] = new Dictionary<string, object?>
            {
                ["qrCodDCe"] = $"{HandBuiltDce.QrBase}?chDCe={chave}&tpAmb={d.TpAmb}",
                ["urlChave"] = HandBuiltDce.QrBase,
            },
        };
        return (dce, chave);
    }
}
