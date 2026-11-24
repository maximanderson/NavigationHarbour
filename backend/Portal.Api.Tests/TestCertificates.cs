using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Portal.Api.Tests;

/// <summary>Creates temporary self-signed certificates (PFX, PEM + key, encrypted PEM key). Nothing is shared or installed.</summary>
public static class TestCertificates
{
    public static X509Certificate2 Create(DateTimeOffset? notBefore = null, DateTimeOffset? notAfter = null)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));

        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("localhost");
        san.AddIpAddress(System.Net.IPAddress.Loopback);
        request.CertificateExtensions.Add(san.Build());

        using var created = request.CreateSelfSigned(
            notBefore ?? DateTimeOffset.UtcNow.AddDays(-1),
            notAfter ?? DateTimeOffset.UtcNow.AddDays(30));

        // Re-import so the private key is usable on every platform.
        return X509CertificateLoader.LoadPkcs12(created.Export(X509ContentType.Pkcs12, "x"), "x", X509KeyStorageFlags.Exportable);
    }

    public static string WritePfx(string path, string? password, X509Certificate2? certificate = null)
    {
        using var owned = certificate is null ? Create() : null;
        var cert = certificate ?? owned!;
        File.WriteAllBytes(path, cert.Export(X509ContentType.Pkcs12, password));
        return path;
    }

    /// <summary>Writes a PEM certificate and its private key; the key is encrypted when a password is given.</summary>
    public static (string Cert, string Key) WritePem(string directory, string name, string? keyPassword, X509Certificate2? certificate = null)
    {
        using var owned = certificate is null ? Create() : null;
        var cert = certificate ?? owned!;

        var certPath = Path.Combine(directory, name + ".pem");
        var keyPath = Path.Combine(directory, name + ".key");

        File.WriteAllText(certPath, cert.ExportCertificatePem());

        using var rsa = cert.GetRSAPrivateKey()!;
        var key = keyPassword is null
            ? rsa.ExportPkcs8PrivateKeyPem()
            : rsa.ExportEncryptedPkcs8PrivateKeyPem(keyPassword, new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 10_000));
        File.WriteAllText(keyPath, key);

        return (certPath, keyPath);
    }
}
