namespace SpikeDce.Tests;

public static class TestEnv
{
    // assets are copied next to the test assembly (see SpikeDce.Tests.csproj content include)
    public static string AssetsDir   => Path.Combine(AppContext.BaseDirectory, "assets");
    public static string DceXsdDir   => Path.Combine(AssetsDir, "xsd", "dce");
    public static string IssuerDir   => Path.Combine(AssetsDir, "issuer");
    public static string AutorizWsdl => Path.Combine(AssetsDir, "wsdl", "homologacao", "DCeAutorizacao.wsdl");

    // The pfx is referenced ONLY by env (absolute path) — never a hardcoded path in source.
    public static string PfxPath =>
        Environment.GetEnvironmentVariable("DCE_SPIKE_PFX_PATH")
        ?? throw new InvalidOperationException(
            "Set DCE_SPIKE_PFX_PATH to the absolute path of the e-CNPJ A1 .pfx before running.");

    public const string HomologStatusUrl  = "https://homologacao.dce.fazenda.pr.gov.br/dce/DCeStatusServico";
    public const string HomologAutorizUrl = "https://homologacao.dce.fazenda.pr.gov.br/dce/DCeAutorizacao";
    public const string ActionStatus  = "http://www.portalfiscal.inf.br/dce/wsdl/DCeStatusServico/dceStatusServico";
    public const string ActionAutoriz = "http://www.portalfiscal.inf.br/dce/wsdl/DCeAutorizacao/dceAutorizacao";
    public const string WsdlNsAutoriz = "http://www.portalfiscal.inf.br/dce/wsdl/DCeAutorizacao";
    public const string WsdlNsStatus  = "http://www.portalfiscal.inf.br/dce/wsdl/DCeStatusServico";
    public const string DceNs = "http://www.portalfiscal.inf.br/dce";

    public static bool SefazEnabled =>
        Environment.GetEnvironmentVariable("DCE_SPIKE_PFX_PASSWORD") is not null
        && Environment.GetEnvironmentVariable("DCE_SPIKE_SKIP_SEFAZ") is null;

    public static string IssuerCnpj => Environment.GetEnvironmentVariable("DCE_SPIKE_CNPJ")!;
}
