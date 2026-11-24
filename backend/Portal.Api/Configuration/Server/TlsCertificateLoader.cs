using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Portal.Api.Configuration.Server;

/// <summary>Raised when the HTTPS certificate cannot be loaded. The message never contains the password.</summary>
public sealed class TlsCertificateException : Exception
{
    public TlsCertificateException(string message) : base(message)
    {
    }
}

/// <summary>
/// Stage 4: loads the HTTPS certificate with .NET's supported APIs, at startup only (diagnostics never call this).
/// cert_file alone is a PFX (PKCS#12); with cert_key_file it is a PEM certificate plus its private key,
/// which may be encrypted (cert_password). Exceptions from the crypto APIs are not passed on, because their
/// messages are not guaranteed to be free of file names or input: only a fixed description and the file are given.
/// </summary>
public static class TlsCertificateLoader
{
    public static X509Certificate2 Load(ServerSettings settings)
    {
        var certFile = settings.CertFilePath ?? throw new TlsCertificateException("[tls] cert_file is not set.");

        try
        {
            using var loaded = settings.CertKeyFilePath is null
                ? LoadPfx(certFile, settings.CertPassword)
                : LoadPem(certFile, settings.CertKeyFilePath, settings.CertPassword);

            Check(loaded);

            // The certificate handed to Kestrel is a separate instance: the loaded one is disposed here.
            return X509CertificateLoader.LoadPkcs12(
                loaded.Export(X509ContentType.Pkcs12, string.Empty), string.Empty, X509KeyStorageFlags.Exportable);
        }
        catch (TlsCertificateException)
        {
            throw;
        }
        catch (CryptographicException) when (settings.CertKeyFilePath is null)
        {
            throw new TlsCertificateException(
                $"The certificate {certFile} could not be loaded as PFX (PKCS#12): the file is not a valid PFX, " +
                (settings.CertPassword is null ? "or it needs a password (set [tls] cert_password)." : "or the password is wrong."));
        }
        catch (CryptographicException)
        {
            throw new TlsCertificateException(
                $"The PEM certificate {certFile} and key {settings.CertKeyFilePath} could not be loaded: the files are not valid PEM, " +
                "the key does not belong to the certificate, " +
                (settings.CertPassword is null ? "or the key is encrypted and needs [tls] cert_password." : "or the key password is wrong."));
        }
        catch (ArgumentException)
        {
            throw new TlsCertificateException(
                $"The PEM certificate {certFile} and key {settings.CertKeyFilePath} could not be loaded: " +
                "a PEM file is missing its certificate or private key block, or the key is encrypted and [tls] cert_password is wrong or not set.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new TlsCertificateException($"The certificate file could not be read ({ex.GetType().Name}): {certFile}");
        }
    }

    private static X509Certificate2 LoadPfx(string file, string? password) =>
        X509CertificateLoader.LoadPkcs12FromFile(file, password, X509KeyStorageFlags.Exportable);

    private static X509Certificate2 LoadPem(string certFile, string keyFile, string? password) =>
        password is null
            ? X509Certificate2.CreateFromPemFile(certFile, keyFile)
            : X509Certificate2.CreateFromEncryptedPemFile(certFile, password, keyFile);

    private static void Check(X509Certificate2 certificate)
    {
        if (!certificate.HasPrivateKey)
        {
            throw new TlsCertificateException("The certificate has no private key; Kestrel cannot use it for HTTPS.");
        }

        var now = DateTime.UtcNow;
        if (certificate.NotAfter.ToUniversalTime() < now)
        {
            throw new TlsCertificateException($"The certificate expired on {certificate.NotAfter:yyyy-MM-dd}.");
        }

        if (certificate.NotBefore.ToUniversalTime() > now)
        {
            throw new TlsCertificateException($"The certificate is not valid before {certificate.NotBefore:yyyy-MM-dd}.");
        }
    }
}
