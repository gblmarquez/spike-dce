using System.Globalization;
using System.Xml;
using System.Xml.Schema;

namespace SpikeDce.Schema;

// Generic, schema-driven builder: walks the compiled particle tree and emits elements in strict XSD order
// from a nested Dictionary<string,object?>. No per-DCe generated classes. (H2)
public sealed class SoapEnvelopeBuilder
{
    private readonly SchemaModel _model;
    public SoapEnvelopeBuilder(SchemaModel model) => _model = model;

    public string BuildDocument(string rootName, string ns, IReadOnlyDictionary<string, object?> data)
    {
        var root = _model.GlobalElement(rootName, ns) ?? throw new InvalidOperationException($"no element {rootName}");
        var sw = new StringWriter();
        using (var w = XmlWriter.Create(sw, new XmlWriterSettings { OmitXmlDeclaration = true }))
            WriteElement(w, root, ns, data);
        return sw.ToString();
    }

    private void WriteElement(XmlWriter w, XmlSchemaElement el, string ns, object? value)
    {
        w.WriteStartElement(el.Name!, ns);
        if (value is IReadOnlyDictionary<string, object?> map)
        {
            if (el.ElementSchemaType is XmlSchemaComplexType ct)
            {
                // attributes first
                foreach (var a in ct.AttributeUses.Values.OfType<XmlSchemaAttribute>())
                    if (map.TryGetValue("@" + a.Name, out var av) && av is not null)
                        w.WriteAttributeString(a.Name!, ToLexical(av));
                if (ct.ContentTypeParticle is XmlSchemaParticle p) WriteParticle(w, p, ns, map);
            }
        }
        else if (value is not null) w.WriteString(ToLexical(value));
        w.WriteEndElement();
    }

    private void WriteParticle(XmlWriter w, XmlSchemaParticle p, string ns, IReadOnlyDictionary<string, object?> map)
    {
        switch (p)
        {
            case XmlSchemaSequence s: foreach (var i in s.Items) WriteParticle(w, (XmlSchemaParticle)i, ns, map); break;
            case XmlSchemaChoice c:   foreach (var i in c.Items) { if (TrySatisfied(w, (XmlSchemaParticle)i, ns, map)) break; } break;
            case XmlSchemaAll a:      foreach (var i in a.Items) WriteParticle(w, (XmlSchemaParticle)i, ns, map); break;
            case XmlSchemaAny:
                // xs:any wildcard — emit all non-attribute entries of the current dict as child elements.
                foreach (var kv in map)
                {
                    if (kv.Key.StartsWith('@') || kv.Value is null) continue;
                    WriteWildcardElement(w, kv.Key, ns, kv.Value);
                }
                break;
            case XmlSchemaElement e:
                var name = e.RefName != null && !e.RefName.IsEmpty
                    ? _model.GlobalElement(e.RefName.Name, e.RefName.Namespace) ?? e : e;
                if (map.TryGetValue(name.Name!, out var v) && v is not null)
                {
                    if (name.MaxOccurs > 1 && v is System.Collections.IEnumerable en && v is not string)
                        foreach (var item in en) WriteElement(w, name, ns, item);
                    else WriteElement(w, name, ns, v);
                }
                // minOccurs==0 with no value => omit (correct). minOccurs>=1 missing => validator flags it in the test.
                break;
        }
    }

    // Writes a schema-free element for xs:any wildcard content, recursing into nested dicts.
    private void WriteWildcardElement(XmlWriter w, string localName, string ns, object value)
    {
        w.WriteStartElement(localName, ns);
        if (value is IReadOnlyDictionary<string, object?> map)
        {
            foreach (var kv in map)
            {
                if (kv.Key.StartsWith('@') && kv.Value is not null)
                    w.WriteAttributeString(kv.Key[1..], ToLexical(kv.Value));
            }
            foreach (var kv in map)
            {
                if (!kv.Key.StartsWith('@') && kv.Value is not null)
                    WriteWildcardElement(w, kv.Key, ns, kv.Value);
            }
        }
        else if (value is not null)
        {
            w.WriteString(ToLexical(value));
        }
        w.WriteEndElement();
    }

    private bool TrySatisfied(XmlWriter w, XmlSchemaParticle p, string ns, IReadOnlyDictionary<string, object?> map)
    {
        if (p is XmlSchemaElement e && map.ContainsKey(e.Name!)) { WriteParticle(w, p, ns, map); return true; }
        return false;
    }

    private static string ToLexical(object v) => v switch
    {
        bool b => b ? "true" : "false",
        DateTime dt => dt.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture),
        DateTimeOffset dto => dto.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => v.ToString() ?? ""
    };
}
