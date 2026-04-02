using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

namespace Shared;

internal static class X509Certificate2CollectionExtensions
{
    extension(X509Certificate2Collection certs)
    {
        public byte[] ToJavaTrustStoreBytes()
        {
            var pkcs12Builder = new Pkcs12Builder();
            var safeContents = new Pkcs12SafeContents();

            // Java's PKCS12 keystore requires the Oracle "Trusted Key Usage" bag attribute
            // (OID 2.16.840.1.113894.746875.1.1) on certificate entries for them to be
            // recognized as trust anchors. Without it, certificates are loaded but not trusted.
            var trustedKeyUsageOid = new Oid("2.16.840.1.113894.746875.1.1");

            var asnWriter = new AsnWriter(AsnEncodingRules.DER);
            asnWriter.WriteObjectIdentifier("2.5.29.37.0"); // anyExtendedKeyUsage
            var anyExtKeyUsageEncoded = asnWriter.Encode();

            foreach (var cert in certs)
            {
                var bag = safeContents.AddCertificate(cert);
                bag.Attributes.Add(new CryptographicAttributeObject(
                    trustedKeyUsageOid,
                    new AsnEncodedDataCollection(
                        new AsnEncodedData(trustedKeyUsageOid, anyExtKeyUsageEncoded))));
            }
            pkcs12Builder.AddSafeContentsUnencrypted(safeContents);
            pkcs12Builder.SealWithMac(string.Empty, HashAlgorithmName.SHA256, 2048);
            return pkcs12Builder.Encode();
        }
    }
}
