using System.Text;

namespace SpikeDce.Dce;

public static class AccessKey
{
    // módulo-11, base 2..9, right-to-left. DV = 11 - resto; if resto in {0,1} => 0.
    public static int Modulo11Dv(string digits43)
    {
        if (digits43.Length != 43) throw new ArgumentException("expected 43 digits", nameof(digits43));
        int sum = 0, weight = 2;
        for (int i = digits43.Length - 1; i >= 0; i--)
        {
            sum += (digits43[i] - '0') * weight;
            weight = weight == 9 ? 2 : weight + 1;
        }
        int resto = sum % 11;
        return resto is 0 or 1 ? 0 : 11 - resto;
    }

    // Compose the 43-digit body then append cDV. Field widths per Visão Geral Tabela 2-1.
    public static string Build(string cUF, string aamm, string cnpj14, string serie3,
                               string nDC9, string tpEmis1, string tpEmit1, string nSite1, string cDC6)
    {
        var body = new StringBuilder();
        body.Append(cUF.PadLeft(2, '0')).Append(aamm).Append(cnpj14)
            .Append("99").Append(serie3.PadLeft(3, '0')).Append(nDC9.PadLeft(9, '0'))
            .Append(tpEmis1).Append(tpEmit1).Append(nSite1).Append(cDC6.PadLeft(6, '0'));
        var b = body.ToString();
        if (b.Length != 43) throw new InvalidOperationException($"key body len={b.Length}, expected 43");
        return b + Modulo11Dv(b);
    }
}
