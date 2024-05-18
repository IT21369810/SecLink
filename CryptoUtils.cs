using System;
using System.Security.Cryptography;
using System.IO;
using System.Text;

namespace SecLinkApp
{
    internal static class CryptoUtils
    {
        // Securely store key or any data
        public static void StoreSecurely(byte[] data, string keyName)
        {
            string folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SecLinkApp", "Keys");
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            string filePath = Path.Combine(folderPath, $"{keyName}.key");

            byte[] encryptedData = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(filePath, encryptedData);
            Console.WriteLine($"Key securely stored at: {filePath}");
        }

        // Retrieve securely stored key or data
        public static byte[] RetrieveSecurely(string keyName)
        {
            string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SecLinkApp", "Keys", $"{keyName}.key");
            if (File.Exists(filePath))
            {
                byte[] encryptedData = File.ReadAllBytes(filePath);
                byte[] data = ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.CurrentUser);
                Console.WriteLine($"Key retrieved from: {filePath}");
                return data;
            }
            else
            {
                Console.WriteLine($"Key not found at: {filePath}");
                return null;
            }
        }
    }
}
