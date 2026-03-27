using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Fedarovich.Aspire.Hosting.MongoDB.ReplicaSet;

/// <summary>
/// Utility class for creating MongoDB certificates.
/// </summary>
public static class MongoDBCertificateUtils
{
    /// <summary>
    /// Creates a self-signed X.509 certificate to be used for securing MongoDB replica set communication.
    /// </summary>
    /// <param name="days">The number of days the certificate remains valid. Must be a positive integer. Defaults to 365 days.</param>
    /// <param name="additionalDnsNames">A set of additional DNS names to include in the Subject Alternative Name (SAN) extension of the certificate. Each entry is added as a DNS name.</param>
    /// <returns>A new <see cref="X509Certificate2"/> instance representing the generated self-signed certificate.</returns>
    public static X509Certificate2 CreateSelfSignedCertificate(int days = 365, params ReadOnlySpan<string> additionalDnsNames)
    {
        using var rsa = RSA.Create(2048);

        var request = new CertificateRequest(
            "CN=localhost",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        // Key usage: digital signature + key encipherment
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                critical: true));

        // Enhanced key usage: TLS server + TLS client authentication
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection
                {
                    new Oid("1.3.6.1.5.5.7.3.1"), // serverAuth
                    new Oid("1.3.6.1.5.5.7.3.2")  // clientAuth
                },
                critical: false));

        // Subject Alternative Name (SAN) — required by modern browsers
        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName("localhost");
        sanBuilder.AddDnsName("*.dev.localhost");
        sanBuilder.AddDnsName("*.dev.internal");
        sanBuilder.AddDnsName("host.docker.internal");
        sanBuilder.AddDnsName("host.containers.internal");
        sanBuilder.AddIpAddress(System.Net.IPAddress.Loopback);       // 127.0.0.1
        sanBuilder.AddIpAddress(System.Net.IPAddress.IPv6Loopback);   // ::1

        foreach (var dnsName in additionalDnsNames)
        {
            sanBuilder.AddDnsName(dnsName);
        }

        request.CertificateExtensions.Add(sanBuilder.Build());

        // Basic constraints: not a CA
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(
                certificateAuthority: false,
                hasPathLengthConstraint: false,
                pathLengthConstraint: 0,
                critical: true));

        var notBefore = DateTimeOffset.UtcNow;
        var notAfter = notBefore.AddDays(days);

        // Create the self-signed certificate
        X509Certificate2 cert = request.CreateSelfSigned(notBefore, notAfter);

        return cert;
    }
}
