using System.Text.Json.Nodes;

namespace SpikeDce.Mapping;

// Thin wrapper over the declarative map JSON: a "derive" array and a "rules" array of JSON objects.
public sealed class MappingSpec
{
    public JsonArray Derive { get; }
    public JsonArray Rules { get; }
    private MappingSpec(JsonArray derive, JsonArray rules) { Derive = derive; Rules = rules; }

    public static MappingSpec Parse(string json)
    {
        var root = JsonNode.Parse(json)!.AsObject();
        return new MappingSpec(
            root["derive"]?.AsArray() ?? new JsonArray(),
            root["rules"]?.AsArray() ?? new JsonArray());
    }

    public static MappingSpec Load(string path) => Parse(File.ReadAllText(path));
}
