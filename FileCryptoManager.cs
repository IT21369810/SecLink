using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using System.IO;
using System.Security.Cryptography;

public class FileCryptoManager
{
    public const int KeySize = 32; // 256 bit key size,  publicly accessible
    public const int IvSize = 12; // Recommended IV size (96 bit) for GCM mode, publicly accessible
    public const int AuthTagSize = 16; // Size of the GCM authentication tag, publicly accessible. 128 bit


    public static void EncryptFile(string inputFile, string outputFile, byte[] key)
    {
        if (key.Length != KeySize)
        {
            throw new ArgumentException($"Key must be {KeySize * 8} bits ({KeySize} bytes) for AES-256.");
        }

        GcmBlockCipher cipher = new GcmBlockCipher(new AesEngine());
        byte[] iv = new byte[IvSize];
        using (var rng = new RNGCryptoServiceProvider())
        {
            rng.GetBytes(iv);
        }

        cipher.Init(true, new AeadParameters(new KeyParameter(key), AuthTagSize * 8, iv));

        byte[] fileContent = File.ReadAllBytes(inputFile);
        byte[] encryptedContent = new byte[cipher.GetOutputSize(fileContent.Length)];
        int len = cipher.ProcessBytes(fileContent, 0, fileContent.Length, encryptedContent, 0);
        len += cipher.DoFinal(encryptedContent, len);

        // Combine IV + encrypted content for output
        byte[] combinedOutput = new byte[IvSize + len];
        Array.Copy(iv, 0, combinedOutput, 0, IvSize);
        Array.Copy(encryptedContent, 0, combinedOutput, IvSize, len);

        File.WriteAllBytes(outputFile, combinedOutput);
        string ivBase64 = Convert.ToBase64String(iv);
        Console.WriteLine($"iv:{ivBase64}");
    }

    public static void DecryptFileDirect(byte[] encryptedContent, string outputFile, byte[] key, byte[] iv)
    {
        //decrypt the files
        if (key.Length != KeySize)
        {
            //key error
            throw new ArgumentException($"Key must be {KeySize * 8} bits ({KeySize} bytes) for 256.");
        }
        if (iv.Length != IvSize)
        {
            //Iv error
            throw new ArgumentException($"IV must be {IvSize} bytes.");
        }
        string ivBase64 = Convert.ToBase64String(iv);
        Console.WriteLine($"iv:{ivBase64}");
        GcmBlockCipher cipher = new GcmBlockCipher(new AesEngine());
        cipher.Init(false, new AeadParameters(new KeyParameter(key), AuthTagSize * 8, iv));

        byte[] decryptedContent = new byte[cipher.GetOutputSize(encryptedContent.Length)];
        int len = cipher.ProcessBytes(encryptedContent, 0, encryptedContent.Length, decryptedContent, 0);
        len += cipher.DoFinal(decryptedContent, len);

        File.WriteAllBytes(outputFile, decryptedContent);
    }

}
