using Bogus;
using Bogus.Extensions.Brazil;
using SpikeDce.Dce;

namespace SpikeDce.Tests;

public static class DceMockData
{
    // Synthetic dest + items + issuance numbers. Seeded for repeatability. Never produces issuer/emit data.
    public static (Party dest, IReadOnlyList<Item> items, decimal vDc,
                   string serie, string nDc, string cDc6, DateTimeOffset dhEmi) Generate(int seed = 20260531)
    {
        var f = new Faker("pt_BR") { Random = new Randomizer(seed) };
        static string Trunc(string s, int n) => s.Length <= n ? s : s[..n];

        var destEnder = new Address(
            XLgr: Trunc(f.Address.StreetName(), 60), Nro: f.Random.Int(1, 4999).ToString(), XCpl: null,
            XBairro: Trunc(f.Address.County(), 60), CMun: "3550308", XMun: "Sao Paulo", UF: "SP",
            CEP: f.Random.Replace("########"), CPais: "1058", XPais: "Brasil",
            Fone: f.Random.Replace("##########"), Email: f.Internet.Email());

        var dest = new Party(Cnpj14: f.Company.Cnpj(includeFormatSymbols: false), Cpf11: null,
            XNome: Trunc(f.Company.CompanyName(), 60), Ender: destEnder);

        int qtd = f.Random.Int(1, 3);
        decimal vUn  = decimal.Round(f.Random.Decimal(10, 500), 2);
        decimal vTot = decimal.Round(vUn * qtd, 2);
        var items = new[] { new Item(Trunc(f.Commerce.ProductName(), 120), "49", qtd, vUn, vTot, InfAdProd: null) };

        return (dest, items, vTot, "0", f.Random.Int(1, 999999).ToString(),
                f.Random.Replace("######"), DateTimeOffset.Now);
    }
}
