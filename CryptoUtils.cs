using System;
using System.IO;
using System.Security.Cryptography;

namespace SecLinkApp
{
    internal static class CryptoUtils
    {
        //generating a hash value for each files
        public static string GenerateHash(string filePath)
        {
            using (var sha256 = SHA256.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    var hashBytes = sha256.ComputeHash(stream);
                    var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                    return hashString;
                }
            }
        }
        
    }
}
