using System;
using System.Security.Cryptography;
using System.Linq;

public class EphemeralKeyGenerator
{
    private readonly byte[] masterKey;
    private const int DefaultKeySize = 32; // Size for 256 bit key

    public EphemeralKeyGenerator(byte[] masterKey)
    {
        if (masterKey == null || masterKey.Length != DefaultKeySize)
        {
            throw new ArgumentException($"Master key must be {DefaultKeySize} bytes long.");
        }

        this.masterKey = masterKey;
    }

    public byte[] GenerateKey(DateTimeOffset timestamp, int keySize = DefaultKeySize)
    {
        if (keySize <= 0 || keySize > DefaultKeySize)
        {
            throw new ArgumentException("Invalid key size.");
        }

        // Use the timestamp to generate a unique key for each session
        byte[] timestampBytes = BitConverter.GetBytes(timestamp.ToUnixTimeSeconds());
        byte[] combined = new byte[masterKey.Length + timestampBytes.Length];
        Buffer.BlockCopy(masterKey, 0, combined, 0, masterKey.Length);
        Buffer.BlockCopy(timestampBytes, 0, combined, masterKey.Length, timestampBytes.Length);

        using (SHA256 sha256 = SHA256.Create())
        {
            // Hash the combined data to produce a fixed size key
            byte[] hashed = sha256.ComputeHash(combined);
            // Return the key of the 256 bit size
            return hashed.Take(keySize).ToArray();

        }
    }
}
