using System.Security.Cryptography.X509Certificates;

namespace SpikeDce.Signing;

public static class CertificateLoader
{
    // Loads the issuer A1 pfx from an absolute path; password from env DCE_SPIKE_PFX_PASSWORD.
    // NOTE: target is net8.0 — X509CertificateLoader is .NET 9+, so use the X509Certificate2 ctor here.
    public static X509Certificate2 LoadFromEnv(string pfxPath)
    {
        var pwd = Environment.GetEnvironmentVariable("DCE_SPIKE_PFX_PASSWORD")
                  ?? throw new InvalidOperationException("Set DCE_SPIKE_PFX_PASSWORD before running.");
        // X509KeyStorageFlags.Exportable so GetRSAPrivateKey() is usable for signing on Linux.
#pragma warning disable SYSLIB0057 // ctor is the supported PKCS12 loader on net8.0
        return new X509Certificate2(
            pfxPath, pwd,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
#pragma warning restore SYSLIB0057
    }
}
