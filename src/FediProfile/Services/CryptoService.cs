using System.Security.Cryptography;
using System.Text;

namespace FediProfile.Services;

public class CryptoService
{
    public class KeyPair
    {
        public string PrivateKeyPem { get; set; } = string.Empty;
        public string PrivateKeyPemClean { get; set; } = string.Empty;
        public string PublicKeyPem { get; set; } = string.Empty;
        public string PublicKeyPemClean { get; set; } = string.Empty;
    }

    public static async Task<KeyPair> GenerateKeyPairAsync()
    {
        return await Task.Run(() =>
        {
            using (RSA rsa = RSA.Create(2048))
            {
                // Export private key in PKCS#8 format
                byte[] privateKeyBytes = rsa.ExportPkcs8PrivateKey();
                string privateKeyPemClean = Convert.ToBase64String(privateKeyBytes);
                string privateKeyPem = FormatPemKey(privateKeyBytes, "PRIVATE KEY");

                // Export public key in SPKI format
                byte[] publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
                string publicKeyPemClean = Convert.ToBase64String(publicKeyBytes);
                string publicKeyPem = FormatPemKey(publicKeyBytes, "PUBLIC KEY");

                return new KeyPair
                {
                    PrivateKeyPem = privateKeyPem,
                    PrivateKeyPemClean = privateKeyPemClean,
                    PublicKeyPem = publicKeyPem,
                    PublicKeyPemClean = publicKeyPemClean
                };
            }
        });
    }

    private static string FormatPemKey(byte[] keyBytes, string keyType)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"-----BEGIN {keyType}-----");

        string base64 = Convert.ToBase64String(keyBytes);
        for (int i = 0; i < base64.Length; i += 64)
        {
            sb.AppendLine(base64.Substring(i, Math.Min(64, base64.Length - i)));
        }

        sb.AppendLine($"-----END {keyType}-----");
        return sb.ToString();
    }
}
