using SpikeDce.Tables;

namespace SpikeDce.Mapping;

// Optional ambient context for transforms needing external data (code-table lookups) + the document date.
public sealed record MapContext(CodeTableRegistry? Tables, DateOnly? Date);
