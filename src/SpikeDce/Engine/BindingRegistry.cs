using System.Text.Json;

namespace SpikeDce.Engine;

public sealed class BindingRegistry
{
    private readonly Dictionary<string, Binding> _byKey;
    private BindingRegistry(Dictionary<string, Binding> byKey) => _byKey = byKey;

    private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };

    public static BindingRegistry Load(string bindingsDir)
    {
        var map = new Dictionary<string, Binding>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(bindingsDir, "*.json", SearchOption.AllDirectories))
        {
            var b = JsonSerializer.Deserialize<Binding>(File.ReadAllText(file), Opts)
                    ?? throw new InvalidOperationException($"unreadable binding: {file}");
            map[Key(b.Model, b.Verb)] = b;
        }
        return new BindingRegistry(map);
    }

    public Binding Resolve(string model, string verb) =>
        _byKey.TryGetValue(Key(model, verb), out var b) ? b
        : throw new InvalidOperationException($"no binding for ({model},{verb})");

    private static string Key(string model, string verb) => $"{model}/{verb}".ToLowerInvariant();
}
