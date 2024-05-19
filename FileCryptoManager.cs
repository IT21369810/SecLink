using Amazon.S3;
using Amazon.S3.Model;
using Amazon;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

public class FileCryptoManager
{
    public const int KeySize = 32; // 256 bits
    public const int IvSize = 16;  // 128 bits for AES-CTR

    //AWS buket details
    private static readonly string bucketName = "seclink";
    private static readonly RegionEndpoint bucketRegion = RegionEndpoint.APSoutheast2;
    //AWS credentials
    private static readonly IAmazonS3 s3Client = new AmazonS3Client("AKIATPCAJ7BYKPWNRXUO", "PdOWA9fqYb2Mwqy2e/drb6w4KZ6+8HCsuT+yB1Jy", bucketRegion);

    //Encrypt the files
    public static void EncryptFile(string inputFile, string outputFile, byte[] key, out byte[] iv)
    {
        if (key.Length != KeySize)
        {
            throw new ArgumentException($"Key must be {KeySize * 8} bits ({KeySize} bytes) for AES-256.");
        }

        var cipher = new BufferedBlockCipher(new SicBlockCipher(new AesEngine())); // BufferedBlockCipher for AES-CTR mode
        iv = new byte[IvSize];
        using (var rng = new RNGCryptoServiceProvider())
        {
            rng.GetBytes(iv);
        }

        cipher.Init(true, new ParametersWithIV(new KeyParameter(key), iv));

        byte[] fileContent = File.ReadAllBytes(inputFile);
        byte[] encryptedContent = new byte[cipher.GetOutputSize(fileContent.Length)];
        int len = cipher.ProcessBytes(fileContent, 0, fileContent.Length, encryptedContent, 0);
        len += cipher.DoFinal(encryptedContent, len);

        byte[] combinedOutput = new byte[IvSize + len];
        Array.Copy(iv, 0, combinedOutput, 0, IvSize);
        Array.Copy(encryptedContent, 0, combinedOutput, IvSize, len);

        File.WriteAllBytes(outputFile, combinedOutput);
    }
    //S3 upload
    public static async Task UploadFileToS3Async(string localFilePath, string s3Key)
    {
        try
        {
            var putRequest = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = s3Key,
                FilePath = localFilePath,
                ContentType = "application/octet-stream"
            };

            var response = await s3Client.PutObjectAsync(putRequest);
        }
        catch (AmazonS3Exception e)
        {
            Console.WriteLine("Error encountered on server. Message:'{0}' when writing an object", e.Message);
        }
        catch (Exception e)
        {
            Console.WriteLine("Unknown encountered on server. Message:'{0}' when writing an object", e.Message);
        }
    }
    //download from S3
    public static async Task DownloadFileFromS3Async(string s3Key, string localFilePath)
    {
        try
        {
            var request = new GetObjectRequest
            {
                BucketName = bucketName,
                Key = s3Key
            };

            using (var response = await s3Client.GetObjectAsync(request))
            using (var responseStream = response.ResponseStream)
            using (var fileStream = File.Create(localFilePath))
            {
                responseStream.CopyTo(fileStream);
            }
        }
        catch (AmazonS3Exception e)
        {
            Console.WriteLine("Error encountered on server. Message:'{0}' when reading an object", e.Message);
        }
        catch (Exception e)
        {
            Console.WriteLine("Unknown encountered on server. Message:'{0}' when reading an object", e.Message);
        }
    }

    public static async Task EncryptAndUploadFileAsync(string inputFile, string s3Key, byte[] key)
    {
        EncryptFile(inputFile, inputFile + ".enc", key, out byte[] iv);
        await UploadFileToS3Async(inputFile + ".enc", s3Key);
        File.Delete(inputFile + ".enc");
    }

    public static async Task DownloadAndDecryptFileAsync(string s3Key, string outputFile, byte[] key)
    {
        string encryptedFilePath = outputFile + ".enc";
        await DownloadFileFromS3Async(s3Key, encryptedFilePath);

        byte[] combinedInput = File.ReadAllBytes(encryptedFilePath);
        byte[] iv = new byte[IvSize];
        byte[] encryptedContent = new byte[combinedInput.Length - IvSize];

        Array.Copy(combinedInput, 0, iv, 0, IvSize);
        Array.Copy(combinedInput, IvSize, encryptedContent, 0, encryptedContent.Length);

        DecryptFileDirect(encryptedContent, outputFile, key, iv);
        File.Delete(encryptedFilePath);
    }

    public static void DecryptFileDirect(byte[] encryptedContent, string outputFile, byte[] key, byte[] iv)
    {
        if (key.Length != KeySize)
        {
            throw new ArgumentException($"Key must be {KeySize * 8} bits ({KeySize} bytes) for AES-256.");
        }
        if (iv.Length != IvSize)
        {
            throw new ArgumentException($"IV must be {IvSize} bytes.");
        }

        var cipher = new BufferedBlockCipher(new SicBlockCipher(new AesEngine())); // BufferedBlockCipher for AES-CTR mode
        cipher.Init(false, new ParametersWithIV(new KeyParameter(key), iv));

        byte[] decryptedContent = new byte[cipher.GetOutputSize(encryptedContent.Length)];
        int len = cipher.ProcessBytes(encryptedContent, 0, encryptedContent.Length, decryptedContent, 0);
        len += cipher.DoFinal(decryptedContent, len);

        File.WriteAllBytes(outputFile, decryptedContent);
    }
}
