using System.Globalization;
using System.Text.Json;
using SpikeDce.Schema;

namespace SpikeDce.Tables;

public sealed record CodeTableVersion(
    string Table, DateOnly EffectiveFrom, string XsdPath,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Rows);

// Versioned reference-table registry. Membership validation delegates to XsdValidator against a generated
// enumeration XSD; this class adds version-by-date selection + enrichment lookup. (Schema-as-data.)
public sealed class CodeTableRegistry
{
    private readonly Dictionary<string, List<CodeTableVersion>> _byTable;
    private CodeTableRegistry(Dictionary<string, List<CodeTableVersion>> byTable) => _byTable = byTable;

    public static CodeTableRegistry Load(string tablesDir)
    {
        var byTable = new Dictionary<string, List<CodeTableVersion>>(StringComparer.OrdinalIgnoreCase);
        foreach (var lookupPath in Directory.EnumerateFiles(tablesDir, "*.lookup.json", SearchOption.AllDirectories))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(lookupPath));
            var root = doc.RootElement;
            var table = root.GetProperty("table").GetString()!;
            var eff = DateOnly.ParseExact(root.GetProperty("effectiveFrom").GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var rows = new Dictionary<string, IReadOnlyDictionary<string, string>>();
            foreach (var row in root.GetProperty("rows").EnumerateObject())
            {
                var fields = new Dictionary<string, string>();
                foreach (var f in row.Value.EnumerateObject()) fields[f.Name] = f.Value.GetString() ?? "";
                rows[row.Name] = fields;
            }
            var xsdPath = Path.Combine(Path.GetDirectoryName(lookupPath)!, eff.ToString("yyyy-MM-dd") + ".xsd");
            if (!byTable.TryGetValue(table, out var list)) byTable[table] = list = new();
            list.Add(new CodeTableVersion(table, eff, xsdPath, rows));
        }
        foreach (var list in byTable.Values) list.Sort((a, b) => b.EffectiveFrom.CompareTo(a.EffectiveFrom));
        return new CodeTableRegistry(byTable);
    }

    public CodeTableVersion Active(string table, DateOnly date)
    {
        if (!_byTable.TryGetValue(table, out var list))
            throw new InvalidOperationException($"unknown code table '{table}'");
        foreach (var v in list) if (v.EffectiveFrom <= date) return v;
        throw new InvalidOperationException($"no '{table}' version effective on or before {date:yyyy-MM-dd}");
    }

    public bool Validate(string table, DateOnly date, string code)
    {
        var v = Active(table, date);
        var dir = Path.GetDirectoryName(v.XsdPath)!;
        var root = Path.GetFileName(v.XsdPath);
        var errors = new XsdValidator(dir, root).Validate($"<{table}>{System.Security.SecurityElement.Escape(code)}</{table}>");
        return errors.Count == 0;
    }

    public string Lookup(string table, DateOnly date, string code, string field)
    {
        var v = Active(table, date);
        if (!v.Rows.TryGetValue(code, out var fields))
            throw new InvalidOperationException($"code '{code}' not in '{table}' ({v.EffectiveFrom:yyyy-MM-dd})");
        if (!fields.TryGetValue(field, out var value))
            throw new InvalidOperationException($"field '{field}' not in '{table}' rows");
        return value;
    }
}
