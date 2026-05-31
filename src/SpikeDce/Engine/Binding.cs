namespace SpikeDce.Engine;

public enum EngineMode { Offline, Live }

public sealed record Binding(
    string Model, string Verb, string RootElement, string Ns, string XsdDir, string RootXsd,
    string Map, string SignedIdPath, string SignatureProfile, string WrapperElement, string WrapperNs,
    string? SoapHeader, string ServiceUrl, string SoapAction, bool Sync);
