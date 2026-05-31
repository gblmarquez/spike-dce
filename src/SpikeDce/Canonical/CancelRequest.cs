namespace SpikeDce.Canonical;

// Author identity + environment + orgao for an event (tpAmb, cOrgao, CNPJAutor).
public sealed record EventContext(string Environment, string OrgaoCode, TaxId Author);

// Canonical "Cancel" verb payload: target DCe access key + its authorization protocol + justification.
public sealed record CancelRequest(
    string AccessKey, string Protocol, string Justification, string Sequence, EventContext Context);
