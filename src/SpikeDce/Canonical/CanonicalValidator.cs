namespace SpikeDce.Canonical;

public static class CanonicalValidator
{
    public static IReadOnlyList<string> Validate(DespatchAdvice d)
    {
        var e = new List<string>();

        void Party(string who, CanonParty? p)
        {
            if (p is null) { e.Add($"{who}: party is required"); return; }
            if (p.TaxId is null || string.IsNullOrWhiteSpace(p.TaxId.Value)) e.Add($"{who}.taxId: required");
            else
            {
                var len = p.TaxId.Value.Length;
                if (p.TaxId.Scheme == "CNPJ" && len != 14) e.Add($"{who}.taxId: CNPJ must be 14 digits");
                else if (p.TaxId.Scheme == "CPF" && len != 11) e.Add($"{who}.taxId: CPF must be 11 digits");
                else if (p.TaxId.Scheme is not ("CNPJ" or "CPF")) e.Add($"{who}.taxId.scheme: must be CNPJ or CPF");
            }
            if (string.IsNullOrWhiteSpace(p.Name)) e.Add($"{who}.name: required");
            if (p.Address is null) e.Add($"{who}.address: required");
            else
            {
                var a = p.Address;
                if (string.IsNullOrWhiteSpace(a.Line)) e.Add($"{who}.address.line: required");
                if (string.IsNullOrWhiteSpace(a.CityCode)) e.Add($"{who}.address.cityCode (IBGE): required");
                if (string.IsNullOrWhiteSpace(a.State)) e.Add($"{who}.address.state (UF): required");
                if (string.IsNullOrWhiteSpace(a.PostalCode)) e.Add($"{who}.address.postalCode: required");
            }
        }

        Party("despatchSupplierParty", d.DespatchSupplierParty);
        Party("deliveryCustomerParty", d.DeliveryCustomerParty);

        if (d.DespatchLines is null || d.DespatchLines.Count == 0)
            e.Add("despatchLines: at least one line is required");

        if (d.Shipment is null) e.Add("shipment: required");
        else if (d.Shipment.DeclaredValueAmount < 0) e.Add("shipment.declaredValueAmount: must be >= 0");

        if (d.Dfe is null) e.Add("dfe: required");
        else if (d.Dfe.Environment is not ("1" or "2")) e.Add("dfe.environment: must be 1 or 2");

        return e;
    }
}
