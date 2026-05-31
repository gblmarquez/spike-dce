namespace SpikeDce.Engine;

public sealed record FiscalEvent(string Model, string Verb, object Payload)
{
    public static FiscalEvent Issue(string model, object document) => new(model, "issue", document);
    public static FiscalEvent Cancel(string model, object request) => new(model, "cancel", request);
}
