using System.Globalization;
using SpikeDce.Dce;

namespace SpikeDce.Mapping;

// Registered primitive toolbox referenced by name from the declarative map. Algorithms (not table data).
public static class Transforms
{
    public static object? Invoke(string fn, IReadOnlyList<object?> args) => fn switch
    {
        "ufToCode"        => UfCodes.ToCode(Str(args[0])),
        "aamm"            => ToDto(args[0]).ToString("yyMM", CultureInfo.InvariantCulture),
        "dateTimeOffset"  => ToDto(args[0]).ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture),
        "decimal"         => ToDec(args[0]).ToString("F" + Convert.ToInt32(args[1]), CultureInfo.InvariantCulture),
        "lastChar"        => Str(args[0])[^1].ToString(),
        "concat"          => string.Concat(args.Select(Str)),
        "qrCode"          => $"https://www.fazenda.pr.gov.br/dce/qrcode?chDCe={Str(args[0])}&tpAmb={Str(args[1])}",
        "accessKey"       => AccessKey.Build(
                                 cUF: Str(args[0]), aamm: Str(args[1]), cnpj14: Str(args[2]),
                                 serie3: Str(args[3]), nDC9: Str(args[4]), tpEmis1: Str(args[5]),
                                 tpEmit1: Str(args[6]), nSite1: Str(args[7]), cDC6: Str(args[8])),
        _ => throw new InvalidOperationException($"unknown transform '{fn}'"),
    };

    static string Str(object? v) => v switch
    {
        null => throw new InvalidOperationException("transform got null arg"),
        DateTimeOffset d => d.ToString("o", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => v.ToString()!,
    };
    static decimal ToDec(object? v) => v is decimal d ? d : decimal.Parse(Str(v), CultureInfo.InvariantCulture);
    static DateTimeOffset ToDto(object? v) => v is DateTimeOffset d ? d : DateTimeOffset.Parse(Str(v), CultureInfo.InvariantCulture);
}
