namespace SpikeDce.Canonical;

// UBL-Despatch-Advice-aligned canonical model (lean DC-e subset). camelCase JSON via SerializeOptions in MappingSpec.
public sealed record TaxId(string Scheme, string Value);          // Scheme: "CNPJ" | "CPF"

public sealed record CanonAddress(
    string Line, string Number, string? Complement, string District,
    string CityCode, string CityName, string State, string PostalCode,
    string CountryCode, string CountryName, string? Phone, string? Email = null);

public sealed record CanonParty(TaxId TaxId, string Name, CanonAddress Address);
public sealed record Carrier(TaxId? TaxId, string? Name);
public sealed record Shipment(Carrier? Carrier, string TransportModeCode, decimal DeclaredValueAmount);

public sealed record DespatchLine(
    string ItemName, string CommodityCode, decimal DeliveredQuantity,
    decimal UnitValue, decimal LineValue, string? Description = null);

public sealed record DfeExtensions(
    string EmissionType, string Series, string Number, string DocumentCode,
    string SiteAuthorizer, string Environment, DateTimeOffset IssuedAt);

public sealed record DespatchAdvice(
    CanonParty DespatchSupplierParty,
    CanonParty DeliveryCustomerParty,
    Shipment Shipment,
    IReadOnlyList<DespatchLine> DespatchLines,
    string? Note,
    DfeExtensions Dfe);
