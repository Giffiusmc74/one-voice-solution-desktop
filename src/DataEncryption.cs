using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace WindowsFormsApp1.src
{
    public static class DataEncryption
    {
        private static readonly byte[] Key; // = GenerateRandomBytes(32); // AES 256-bit key
        
        static DataEncryption()
        {          
            // Convert string to bytes
            byte[] keyBytes = Encoding.UTF8.GetBytes("OneApp");

            // Resize the array to 32 bytes (256 bits) for AES-256
            Array.Resize(ref keyBytes, 32);

            Key = keyBytes;
        }

        private static byte[] GenerateRandomBytes(int length)
        {
            using (var rng = new RNGCryptoServiceProvider())
            {
                byte[] randomBytes = new byte[length];
                rng.GetBytes(randomBytes);
                return randomBytes;
            }
        }

        public static string EncryptString_Aes(string plainText)
        {
            byte[] IV = new byte[16];//GenerateRandomBytes(16); // AES block size is 128-bit

            byte[] encrypted;
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = Key;
                aesAlg.IV = IV;

                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);
                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            swEncrypt.Write(plainText);
                        }
                        encrypted = msEncrypt.ToArray();
                    }
                }
            }

            // Combine the encrypted data with the IV and return as Base64-encoded string
            byte[] combinedData = new byte[IV.Length + encrypted.Length];
            Array.Copy(IV, 0, combinedData, 0, IV.Length);
            Array.Copy(encrypted, 0, combinedData, IV.Length, encrypted.Length);

            return Convert.ToBase64String(combinedData);
        }

        public static string DecryptString_Aes(string cipherTextString)
        {
            byte[] combinedData = Convert.FromBase64String(cipherTextString);
            byte[] IV = new byte[16];
            byte[] cipherText = new byte[combinedData.Length - IV.Length];

            Array.Copy(combinedData, 0, IV, 0, IV.Length);
            Array.Copy(combinedData, IV.Length, cipherText, 0, cipherText.Length);

            string plaintext = null;

            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = Key;
                aesAlg.IV = IV;

                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);
                using (MemoryStream msDecrypt = new MemoryStream(cipherText))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                        {
                            plaintext = srDecrypt.ReadToEnd();
                        }
                    }
                }
            }

            return plaintext;
        }
    }
}
