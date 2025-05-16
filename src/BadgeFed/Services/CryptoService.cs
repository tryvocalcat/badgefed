using System.Security.Cryptography;
using System.Text;
using BadgeFed.Models;

namespace BadgeFed.Services;

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

            var keyPair = new KeyPair
            {
                PrivateKeyPem = privateKeyPem,
                PrivateKeyPemClean = privateKeyPemClean,
                PublicKeyPem = publicKeyPem,
                PublicKeyPemClean = publicKeyPemClean
            };

            return keyPair;
        }
    }

    private static string FormatPemKey(byte[] keyBytes, string keyType)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"-----BEGIN {keyType}-----");
        
        // Insert line breaks every 64 characters
        string base64 = Convert.ToBase64String(keyBytes);
        for (int i = 0; i < base64.Length; i += 64)
        {
            sb.AppendLine(base64.Substring(i, Math.Min(64, base64.Length - i)));
        }
        
        sb.AppendLine($"-----END {keyType}-----");
        return sb.ToString();
    }
}