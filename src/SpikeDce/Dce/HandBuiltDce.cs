using System.Globalization;
using System.Xml.Linq;

namespace SpikeDce.Dce;

public static class HandBuiltDce
{
    public const string Ns = "http://www.portalfiscal.inf.br/dce";
    const string Mod = "99", TpEmis = "1", TpEmit = "2", NSite = "0", VerProc = "SpikeDce/1.0";
    public const string QrBase = "https://www.fazenda.pr.gov.br/dce/qrcode";

    // Fixed legal texts required in infDec (per dceTiposBasico_v1.00.xsd documentation). Public so the
    // Phase-1 dict fixture reuses the exact same values (no drift between hand-built and engine-built).
    public const string XObs1 = "É contribuinte de ICMS qualquer pessoa física ou jurídica, que realize, com habitualidade ou em volume que caracterize intuito comercial, operações de circulação de mercadoria ou prestações de serviços de transportes interestadual e intermunicipal e de comunicação, ainda que as operações e prestações de iniciem no exterior (Lei Complementar nº 87/96, Art. 4º)";
    public const string XObs2 = "Constitui crime contra a ordem tributária suprimir ou reduzir tributo, ou contribuição social e qualquer acessório: quando negar ou deixar de fornecer, quando obrigatório, nota fiscal ou documento equivalente, relativa a venda de mercadoria ou prestação de serviço, efetivamente realizada ou fornece-la em desacordo com a legislação. Sob pena de reclusão de 2 (dois) e 5 (cinco) anos, e multa (Lei 8.137/90, Art 1ª, V)";

    // Returns (unsigned DCe xml, accessKey). For Emissão Própria (tpEmit=2) the issuer-type choice is omitted.
    public static (string xml, string chave) Build(DceData d)
    {
        static string Dec(decimal v, int dec) => v.ToString("F" + dec, CultureInfo.InvariantCulture);
        var aamm  = d.DhEmi.ToString("yyMM", CultureInfo.InvariantCulture);
        var chave = AccessKey.Build(cUF: d.Issuer.CUF, aamm: aamm, cnpj14: d.Issuer.Cnpj14,
            serie3: d.Serie, nDC9: d.NDc, tpEmis1: TpEmis, tpEmit1: TpEmit, nSite1: NSite, cDC6: d.CDc6);
        var cDV = chave[^1].ToString();
        XNamespace x = Ns;

        XElement Ender(string tag, Address a)
        {
            var e = new XElement(x + tag, new XElement(x + "xLgr", a.XLgr), new XElement(x + "nro", a.Nro));
            if (!string.IsNullOrWhiteSpace(a.XCpl)) e.Add(new XElement(x + "xCpl", a.XCpl));
            e.Add(new XElement(x + "xBairro", a.XBairro), new XElement(x + "cMun", a.CMun),
                  new XElement(x + "xMun", a.XMun), new XElement(x + "UF", a.UF),
                  new XElement(x + "CEP", a.CEP), new XElement(x + "cPais", a.CPais), new XElement(x + "xPais", a.XPais));
            if (!string.IsNullOrWhiteSpace(a.Fone)) e.Add(new XElement(x + "fone", a.Fone));
            if (tag == "enderDest" && !string.IsNullOrWhiteSpace(a.Email)) e.Add(new XElement(x + "email", a.Email));
            return e;
        }

        var ide = new XElement(x + "ide",
            new XElement(x + "cUF", d.Issuer.CUF), new XElement(x + "cDC", d.CDc6),
            new XElement(x + "mod", Mod), new XElement(x + "serie", d.Serie), new XElement(x + "nDC", d.NDc),
            new XElement(x + "dhEmi", d.DhEmi.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture)),
            new XElement(x + "tpEmis", TpEmis), new XElement(x + "tpEmit", TpEmit),
            new XElement(x + "nSiteAutoriz", NSite), new XElement(x + "cDV", cDV),
            new XElement(x + "tpAmb", d.TpAmb), new XElement(x + "verProc", VerProc));

        var emit = new XElement(x + "emit",
            new XElement(x + "CNPJ", d.Emit.Cnpj14), new XElement(x + "xNome", d.Emit.XNome),
            Ender("enderEmit", d.Emit.Ender));

        var destId = d.Dest.Cnpj14 is not null ? new XElement(x + "CNPJ", d.Dest.Cnpj14)
                                               : new XElement(x + "CPF", d.Dest.Cpf11);
        var dest = new XElement(x + "dest", destId, new XElement(x + "xNome", d.Dest.XNome),
            Ender("enderDest", d.Dest.Ender));

        var det = d.Items.Select((it, i) => new XElement(x + "det", new XAttribute("nItem", (i + 1).ToString()),
            new XElement(x + "prod",
                new XElement(x + "xProd", it.XProd), new XElement(x + "NCM", it.Ncm),
                new XElement(x + "qCom", Dec(it.QCom, 4)), new XElement(x + "vUnCom", Dec(it.VUnCom, 2)),
                new XElement(x + "vProd", Dec(it.VProd, 2))),
            it.InfAdProd is null ? null : new XElement(x + "infAdProd", it.InfAdProd)));

        var total  = new XElement(x + "total", new XElement(x + "vDC", Dec(d.VDc, 2)));
        var transp = new XElement(x + "transp", new XElement(x + "modTrans", d.ModTrans));
        if (!string.IsNullOrWhiteSpace(d.CnpjTransp)) transp.Add(new XElement(x + "CNPJTransp", d.CnpjTransp));

        var infDec = new XElement(x + "infDec",
            new XElement(x + "xObs1", XObs1), new XElement(x + "xObs2", XObs2));

        var infDce = new XElement(x + "infDCe", new XAttribute("Id", "DCe" + chave), new XAttribute("versao", "1.00"),
            ide, emit, dest, det, total, transp, infDec);

        // Required tail of TDCe: infSolicDCe → infDCeSupl → (ds:Signature appended by the signer).
        var infSolic = new XElement(x + "infSolicDCe",
            new XElement(x + "xSolic", "Emissao propria de DC-e para transporte de bens."));
        var qr = $"{QrBase}?chDCe={chave}&tpAmb={d.TpAmb}";
        var supl = new XElement(x + "infDCeSupl",
            new XElement(x + "qrCodDCe", qr), new XElement(x + "urlChave", QrBase));

        var dce = new XElement(x + "DCe", infDce, infSolic, supl);
        return (dce.ToString(SaveOptions.DisableFormatting), chave);
    }
}
