namespace SpikeDce.Dce;

public sealed record Address(string XLgr, string Nro, string? XCpl, string XBairro,
                             string CMun, string XMun, string UF, string CEP,
                             string CPais, string XPais, string? Fone, string? Email = null);

public sealed record EmitCompany(string Cnpj14, string XNome, Address Ender);
public sealed record Party(string? Cnpj14, string? Cpf11, string XNome, Address Ender);
public sealed record Item(string XProd, string Ncm, decimal QCom, decimal VUnCom, decimal VProd, string? InfAdProd);

// Resolved issuer identity: CUF/UF are non-null here (resolved from the real emit UF or env override).
public sealed record ResolvedIssuer(string Cnpj14, string CUF, string UF);

public sealed record DceData(
    ResolvedIssuer Issuer, EmitCompany Emit, Party Dest, IReadOnlyList<Item> Items,
    decimal VDc, string ModTrans, string? CnpjTransp, DateTimeOffset DhEmi,
    string Serie, string NDc, string CDc6, string TpAmb);
