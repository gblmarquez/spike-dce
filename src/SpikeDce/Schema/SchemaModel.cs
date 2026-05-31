using System.Xml;
using System.Xml.Schema;

namespace SpikeDce.Schema;

// Runtime index over the compiled DC-e XmlSchemaSet — the "schema as data" core of the engine.
public sealed class SchemaModel
{
    public XmlSchemaSet Set { get; }
    private SchemaModel(XmlSchemaSet set) => Set = set;

    public static SchemaModel Load(string xsdDir, string rootXsd = "dce_v1.00.xsd")
    {
        var set = new XmlSchemaSet { XmlResolver = new XmlUrlResolver() };
        using var r = XmlReader.Create(Path.Combine(xsdDir, rootXsd)); // includes resolve relatively
        set.Add(null, r);
        set.Compile();
        return new SchemaModel(set);
    }

    public XmlSchemaElement? GlobalElement(string name, string ns)
        => Set.GlobalElements[new XmlQualifiedName(name, ns)] as XmlSchemaElement;
}
