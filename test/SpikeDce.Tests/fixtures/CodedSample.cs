namespace SpikeDce.Tests;

// Synthetic NF-e-like canonical fragment with coded fields (DC-e has none) — exercises the validator hook.
public sealed record CodedSample(TaxLine TaxLine, Payment Payment);
public sealed record TaxLine(string ClassificationCode);
public sealed record Payment(string MethodCode);
