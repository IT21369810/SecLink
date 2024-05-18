using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using System.Security.Cryptography;

public static class SecureEphemeralKeyExchange
{
    public static byte[] EncryptEphemeralKey(byte[] ephemeralKey, byte[] sharedSecret, out byte[] iv)
    {
        // GCM uses a 256 bit key.
        byte[] aesKey = new byte[32];
        Array.Copy(sharedSecret, aesKey, aesKey.Length);

        // Generate a random IV
        iv = new byte[12]; // GCM recommends a 12-byte IV for efficiency and security
        using (var rng = new RNGCryptoServiceProvider())
        {
            rng.GetBytes(iv);
        }

        GcmBlockCipher cipher = new GcmBlockCipher(new AesEngine());
        AeadParameters parameters = new AeadParameters(new KeyParameter(aesKey), 128, iv); // 128 bit auth tag length

        cipher.Init(true, parameters);

        byte[] encryptedKey = new byte[cipher.GetOutputSize(ephemeralKey.Length)];
        int len = cipher.ProcessBytes(ephemeralKey, 0, ephemeralKey.Length, encryptedKey, 0);
        cipher.DoFinal(encryptedKey, len);

        return encryptedKey;
    }

    public static byte[] DecryptEphemeralKey(byte[] encryptedKey, byte[] sharedSecret, byte[] iv)
    {
        // GCM uses a 256 bit key.
        byte[] aesKey = new byte[32];
        Array.Copy(sharedSecret, aesKey, aesKey.Length);

        GcmBlockCipher cipher = new GcmBlockCipher(new AesEngine());
        AeadParameters parameters = new AeadParameters(new KeyParameter(aesKey), 128, iv); // 128 bit auth tag length

        cipher.Init(false, parameters);

        byte[] decryptedKey = new byte[cipher.GetOutputSize(encryptedKey.Length)];
        int len = cipher.ProcessBytes(encryptedKey, 0, encryptedKey.Length, decryptedKey, 0);
        cipher.DoFinal(decryptedKey, len);

        return decryptedKey;
    }
}
