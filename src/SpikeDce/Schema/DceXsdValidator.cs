using System.Xml;
using System.Xml.Schema;

namespace SpikeDce.Schema;

public sealed class DceXsdValidator
{
    private readonly XmlSchemaSet _set;
    public DceXsdValidator(string dceXsdDir)
    {
        _set = new XmlSchemaSet { XmlResolver = new XmlUrlResolver() };
        using var r = XmlReader.Create(Path.Combine(dceXsdDir, "dce_v1.00.xsd")); // includes resolve relatively
        _set.Add(null, r);
        _set.Compile();
    }

    public IReadOnlyList<string> Validate(string xml)
    {
        var errors = new List<string>();
        var settings = new XmlReaderSettings { ValidationType = ValidationType.Schema };
        settings.Schemas.Add(_set);
        settings.ValidationEventHandler += (_, e) => errors.Add($"{e.Severity}: {e.Message}");
        using var sr = new StringReader(xml);
        using var vr = XmlReader.Create(sr, settings);
        while (vr.Read()) { }
        return errors;
    }
}
