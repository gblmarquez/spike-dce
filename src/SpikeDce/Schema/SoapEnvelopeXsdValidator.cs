using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace SpikeDce.Schema;

// Validates the SOAP body wrapper dceDadosMsg AND, because its <s:any> is processContents="strict",
// the inner DCe against dce_v1.00.xsd — all in one XmlSchemaSet.
public sealed class SoapEnvelopeXsdValidator
{
    private readonly XmlSchemaSet _set;
    public SoapEnvelopeXsdValidator(string autorizWsdlPath, string dceXsdDir)
    {
        _set = new XmlSchemaSet { XmlResolver = new XmlUrlResolver() };
        // 1) inner DC-e schema tree (dce_v1.00.xsd → includes resolve relatively)
        using (var r = XmlReader.Create(Path.Combine(dceXsdDir, "dce_v1.00.xsd"))) _set.Add(null, r);
        // 2) WSDL-embedded schema(s): the dceDadosMsg wrapper element in the …/wsdl/DCeAutorizacao ns
        XNamespace wsdl = "http://schemas.xmlsoap.org/wsdl/";
        XNamespace xs   = "http://www.w3.org/2001/XMLSchema";
        var doc = XDocument.Load(autorizWsdlPath);
        foreach (var schema in doc.Descendants(wsdl + "types").Elements(xs + "schema"))
            using (var sr = schema.CreateReader())
                _set.Add(null, sr);
        _set.Compile();
    }

    // dceDadosMsgXml = <dceDadosMsg xmlns="…/wsdl/DCeAutorizacao"><DCe xmlns="…/dce">…</DCe></dceDadosMsg>
    public IReadOnlyList<string> ValidateEnvelope(string dceDadosMsgXml)
    {
        var errors = new List<string>();
        var settings = new XmlReaderSettings { ValidationType = ValidationType.Schema };
        settings.Schemas.Add(_set);
        settings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
        settings.ValidationEventHandler += (_, e) => errors.Add($"{e.Severity}: {e.Message}");
        using var sr = new StringReader(dceDadosMsgXml);
        using var vr = XmlReader.Create(sr, settings);
        while (vr.Read()) { }
        return errors;
    }
}
