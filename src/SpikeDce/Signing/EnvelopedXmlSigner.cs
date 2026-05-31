using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;

namespace SpikeDce.Signing;

public static class EnvelopedXmlSigner
{
    // Enveloped signature over the element with the given Id (infDCe). rsa-sha1 / sha1 / C14N, EndCertOnly, no KeyValue.
    // The <Signature> is appended as the last child of the document element (DCe), per TDCe sequence.
    public static string SignEnveloped(string xml, string referenceId, X509Certificate2 cert)
    {
        var doc = new XmlDocument { PreserveWhitespace = true };
        doc.LoadXml(xml);

        using var rsa = cert.GetRSAPrivateKey() ?? throw new InvalidOperationException("no private key");
        var signed = new SignedXml(doc) { SigningKey = rsa };
        signed.SignedInfo!.CanonicalizationMethod = SignedXml.XmlDsigC14NTransformUrl; // REC-xml-c14n-20010315
        signed.SignedInfo.SignatureMethod = SignedXml.XmlDsigRSASHA1Url;               // rsa-sha1

        var reference = new Reference("#" + referenceId) { DigestMethod = SignedXml.XmlDsigSHA1Url };
        reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        reference.AddTransform(new XmlDsigC14NTransform());
        signed.AddReference(reference);

        var keyInfo = new KeyInfo();
        keyInfo.AddClause(new KeyInfoX509Data(cert)); // EndCertOnly (cert only, no KeyValue)
        signed.KeyInfo = keyInfo;

        signed.ComputeSignature();
        var sigElem = signed.GetXml();
        doc.DocumentElement!.AppendChild(doc.ImportNode(sigElem, true));
        return doc.OuterXml; // byte-stable string; do not re-serialize via XElement later
    }
}
