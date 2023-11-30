using System.Security.Cryptography;
using System.Text;

namespace PraticaSocketsCliente
{
    public class CryptoManager
    {
        private ECDiffieHellmanCng _diffieHellman = new();
        private RSACryptoServiceProvider _rsa = new();
        public byte[] PublicKey => _diffieHellman.PublicKey.ToByteArray();

        public string RSAPublicKey => _rsa.ToXmlString(false);

        public CryptoManager()
        {
            _diffieHellman.KeyDerivationFunction = ECDiffieHellmanKeyDerivationFunction.Hash;
            _diffieHellman.HashAlgorithm = CngAlgorithm.Sha256;
        }

        public byte[] GetSharedSecret(byte[] otherPartyPublicKey)
        {
            return _diffieHellman.DeriveKeyMaterial(CngKey.Import(otherPartyPublicKey, CngKeyBlobFormat.EccPublicBlob));
        }

        public dynamic Encrypt(string data, byte[] serverPublicKey)
        {
            using Aes aes = new AesCryptoServiceProvider();
            aes.Key = GetSharedSecret(serverPublicKey);
            var iv = aes.IV;

            // Encrypt the message
            using MemoryStream ciphertext = new();
            using CryptoStream cs = new(ciphertext, aes.CreateEncryptor(), CryptoStreamMode.Write);
            byte[] plaintextMessage = Encoding.UTF8.GetBytes(data);
            cs.Write(plaintextMessage, 0, plaintextMessage.Length);
            cs.Close();
            return new { encryptedMessage = ciphertext.ToArray(), IV = iv };
        }

        public dynamic Encrypt(byte[] data, byte[] serverPublicKey)
        {
            using Aes aes = new AesCryptoServiceProvider();
            aes.Key = GetSharedSecret(serverPublicKey);
            var iv = aes.IV;

            // Encrypt the message
            using MemoryStream ciphertext = new();
            using CryptoStream cs = new(ciphertext, aes.CreateEncryptor(), CryptoStreamMode.Write);
            cs.Write(data, 0, data.Length);
            cs.Close();
            return new { encryptedMessage = ciphertext.ToArray(), IV = iv };
        }

        public string Decrypt(byte[] data, byte[] iv, byte[] serverPublicKey)
        {
            using Aes aes = new AesCryptoServiceProvider();
            aes.Key = GetSharedSecret(serverPublicKey);
            aes.IV = iv;
            // Decrypt the message
            using MemoryStream plaintext = new();
            using CryptoStream cs = new(plaintext, aes.CreateDecryptor(), CryptoStreamMode.Write);
            cs.Write(data, 0, data.Length);
            cs.Close();
            return Encoding.UTF8.GetString(plaintext.ToArray());
        }

        public byte[] Sign(byte[] data)
        {
            return _rsa.SignData(data, SHA256.Create());
        }

        public string GetRSAParameters()
        {
            return _rsa.ToXmlString(false);
        }
    }
}