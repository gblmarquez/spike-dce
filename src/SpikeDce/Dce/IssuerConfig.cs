namespace SpikeDce.Dce;

// Issuer identity for the spike — read from env at run time, never hardcoded/committed.
// CUF/UF are optional overrides; when null the fixture derives them from the real emit company UF.
public sealed record IssuerConfig(string Cnpj14, string? XNomeOverride, string? CUF, string? UF)
{
    public static IssuerConfig FromEnv()
    {
        static string Req(string k) =>
            Environment.GetEnvironmentVariable(k) ?? throw new InvalidOperationException($"Set {k} before running.");
        var cnpj = new string(Req("DCE_SPIKE_CNPJ").Where(char.IsDigit).ToArray());
        if (cnpj.Length != 14) throw new InvalidOperationException("DCE_SPIKE_CNPJ must be 14 digits.");
        var xNome = Environment.GetEnvironmentVariable("DCE_SPIKE_XNOME"); // optional override
        var cUF   = Environment.GetEnvironmentVariable("DCE_SPIKE_CUF");   // optional override
        var uf    = Environment.GetEnvironmentVariable("DCE_SPIKE_UF");    // optional override
        return new IssuerConfig(cnpj, xNome, cUF, uf);
    }
}
