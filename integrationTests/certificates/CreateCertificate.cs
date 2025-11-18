using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.IO;

class Program
{
    static void Main(string[] args)
    {
        // Create a self-signed certificate
        using (RSA rsa = RSA.Create(2048))
        {
            var request = new CertificateRequest(
                "CN=localhost",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            // Add extensions
            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(false, false, 0, false));

            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                    false));

            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    new OidCollection
                    {
                        new Oid("1.3.6.1.5.5.7.3.1") // Server Authentication
                    },
                    false));

            request.CertificateExtensions.Add(
                new X509SubjectAlternativeNameExtension(
                    new System.Collections.Generic.List<string> { "localhost" }));

            var certificate = request.CreateSelfSigned(
                DateTimeOffset.Now.AddDays(-1),
                DateTimeOffset.Now.AddYears(1));

            // Export as PFX
            var pfxBytes = certificate.Export(X509ContentType.Pfx, "testpassword");
            File.WriteAllBytes("localhost.pfx", pfxBytes);

            // Export as CER for trust store
            var cerBytes = certificate.Export(X509ContentType.Cert);
            File.WriteAllBytes("localhost.cer", cerBytes);

            Console.WriteLine("Certificate created successfully:");
            Console.WriteLine("  PFX: localhost.pfx");
            Console.WriteLine("  CER: localhost.cer");
            Console.WriteLine("  Password: testpassword");
        }
    }
}
