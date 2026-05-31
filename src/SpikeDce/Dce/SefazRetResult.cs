using System.Xml.Linq;

namespace SpikeDce.Dce;

public sealed record SefazRetResult(string CStat, string XMotivo, string? Protocolo, string Raw)
{
    public static SefazRetResult Parse(string responseXml)
    {
        XNamespace d = "http://www.portalfiscal.inf.br/dce";
        try
        {
            var x = XDocument.Parse(responseXml);
            string? F(string n) => x.Descendants(d + n).FirstOrDefault()?.Value
                                    ?? x.Descendants().FirstOrDefault(e => e.Name.LocalName == n)?.Value;
            return new SefazRetResult(F("cStat") ?? "(none)", F("xMotivo") ?? "", F("nProt"), responseXml);
        }
        catch
        {
            return new SefazRetResult("(unparseable)", "", null, responseXml);
        }
    }
}
