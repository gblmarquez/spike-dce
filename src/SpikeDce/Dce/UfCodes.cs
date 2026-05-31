namespace SpikeDce.Dce;

// IBGE UF → 2-digit cUF code. Used to derive ide/cUF (and access-key cUF) from the issuer's real UF.
public static class UfCodes
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RO"]="11",["AC"]="12",["AM"]="13",["RR"]="14",["PA"]="15",["AP"]="16",["TO"]="17",
        ["MA"]="21",["PI"]="22",["CE"]="23",["RN"]="24",["PB"]="25",["PE"]="26",["AL"]="27",["SE"]="28",["BA"]="29",
        ["MG"]="31",["ES"]="32",["RJ"]="33",["SP"]="35",
        ["PR"]="41",["SC"]="42",["RS"]="43",
        ["MS"]="50",["MT"]="51",["GO"]="52",["DF"]="53",
    };

    public static string ToCode(string uf) =>
        Map.TryGetValue(uf, out var c) ? c : throw new ArgumentException($"unknown UF '{uf}'", nameof(uf));
}
