using System.Text.Json;
using SpikeDce.Dce;

namespace SpikeDce.Tests;

// EMIT (issuer) data by CNPJ. Source: committed BrasilAPI fixture assets/issuer/<cnpj>.json (fetched once via curl).
// No live HTTP here — keeps a single integration point (SEFAZ) in the suite. Prod analogue: dfetech-tax-payers-api.
public static class CompanyDirectory
{
    public static EmitCompany Lookup(string cnpj14, string? xNomeOverride = null)
    {
        var path = Path.Combine(TestEnv.IssuerDir, cnpj14 + ".json");
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"Issuer fixture missing. Run: curl -fsS \"https://brasilapi.com.br/api/cnpj/v1/{cnpj14}\" " +
                $"-o assets/issuer/{cnpj14}.json", path);

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var j = doc.RootElement;
        string S(string n) => j.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";
        string Num(string n) => j.TryGetProperty(n, out var v)
            ? (v.ValueKind == JsonValueKind.Number ? v.GetRawText() : (v.GetString() ?? "")) : "";
        static string Digits(string s) => new string((s ?? "").Where(char.IsDigit).ToArray());
        static string Trunc(string s, int n) => string.IsNullOrEmpty(s) ? s : (s.Length <= n ? s : s[..n]);

        var ender = new Address(
            XLgr: Trunc(S("logradouro"), 60), Nro: string.IsNullOrWhiteSpace(S("numero")) ? "S/N" : Trunc(S("numero"), 60),
            XCpl: string.IsNullOrWhiteSpace(S("complemento")) ? null : Trunc(S("complemento"), 60),
            XBairro: Trunc(S("bairro"), 60), CMun: Num("codigo_municipio_ibge"), XMun: Trunc(S("municipio"), 60),
            UF: S("uf"), CEP: Digits(S("cep")), CPais: "1058", XPais: "Brasil",
            Fone: string.IsNullOrWhiteSpace(Digits(S("ddd_telefone_1"))) ? null : Digits(S("ddd_telefone_1")));
        var xNome = Trunc(xNomeOverride ?? S("razao_social"), 60);
        return new EmitCompany(cnpj14, xNome, ender);
    }
}
