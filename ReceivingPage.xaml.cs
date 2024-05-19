using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Windows.Security.Credentials;

namespace SecLinkApp
{
    public partial class ReceivingPage : Page
    {
        public ReceivingPage()
        {
            InitializeComponent();
        }

        private void RetrieveButton_Click(object sender, RoutedEventArgs e)
        {
            var hashValue = HashTextBox.Text.Trim();

            if (string.IsNullOrEmpty(hashValue))
            {
                MessageBox.Show("Please enter a hash value.");
                return;
            }

            try
            {
                var (encryptedPath, ivBase64, authLevel, timestamp) = DatabaseHelper.GetFileMetadataByHash(hashValue);
                Console.WriteLine($"1");

                byte[] iv = Convert.FromBase64String(ivBase64);
                Console.WriteLine($"2");
                if (authLevel == "Double")
                {
                    Console.WriteLine($"3");
                    var faceAuthWindow = new FaceAuthenticationWindow();
                    bool? faceAuthResult = faceAuthWindow.ShowDialog();
                    if (faceAuthResult != true)
                    {
                        MessageBox.Show("Face authentication failed. Please try again.");
                        return;
                    }
                }

                bool helloAuthResult = AuthenticateWithWindowsHello();
                Console.WriteLine($"4");
                if (!helloAuthResult)
                {
                    MessageBox.Show("Windows Hello authentication failed. Please try again.");
                    return;
                }

                byte[] masterKey = RetrieveSecureMasterKey();
                Console.WriteLine($"5");

                if (masterKey == null)
                {
                    Console.WriteLine($"6");
                    MessageBox.Show("Failed to retrieve the master key. Decryption cannot proceed.");
                    Console.WriteLine($"7");
                    return;
                }
                Console.WriteLine($"8");
                byte[] encryptedContent = File.ReadAllBytes(encryptedPath);
                if (encryptedContent == null) // Check if originalFileName is not null
                {
                    Console.WriteLine($"9");
                    DownloadFileFromS3(encryptedPath);
                }
                Console.WriteLine($"10");
                string outputDirectory = GetSaveDirectoryPath();
                
                string originalFileName = Path.GetFileName(encryptedPath).Replace(".enc", "");
                

                if (string.IsNullOrEmpty(outputDirectory))
                {
                    
                    MessageBox.Show("File save operation was cancelled.");
                    return;
                }

                string outputFilePath = Path.Combine(outputDirectory, originalFileName);
                DateTimeOffset timestampParsed = DateTimeOffset.Parse(timestamp);
                
                DecryptFile(encryptedContent, outputFilePath, masterKey, iv, timestampParsed);

                Console.WriteLine($"Hash Value: {hashValue}");
                Console.WriteLine($"Encrypted File Path: {encryptedPath}");
                Console.WriteLine($"IV: {Convert.ToBase64String(iv)}");
                Console.WriteLine($"Output File Path: {outputFilePath}");

                MessageBox.Show("File decrypted and saved successfully.");

            }
            catch (FileNotFoundException)
            {
                // If the file is not found locally, download it from the bucket

                
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while retrieving the file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private async void DownloadFileFromS3(string encryptedPath)
        {
            try
            {
                Console.WriteLine($"Output File Path: {encryptedPath}");
                string originalFileName = Path.GetFileName(encryptedPath).Replace(".enc", "");
                Console.WriteLine($"Output File Path: {originalFileName}");
                // Define the local file path to save the downloaded file
                string localFilePath = Path.Combine(GetSaveDirectoryPath(), originalFileName);

                // Download the file from S3
                await FileCryptoManager.DownloadFileFromS3Async(originalFileName, localFilePath);

                MessageBox.Show("File downloaded from Amazon S3.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                // Now that the file is downloaded, decrypt it
                byte[] masterKey = RetrieveSecureMasterKey();
                byte[] encryptedContent = File.ReadAllBytes(localFilePath);
                byte[] iv = new byte[FileCryptoManager.IvSize];
                Array.Copy(encryptedContent, 0, iv, 0, FileCryptoManager.IvSize);
                string decryptedFilePath = localFilePath.Replace(".enc", "");
                DateTimeOffset timestamp = DateTimeOffset.UtcNow; // You may need to retrieve the timestamp from the database or another source
                DecryptFile(encryptedContent, decryptedFilePath, masterKey, iv, timestamp);

                MessageBox.Show("File decrypted and saved locally.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while downloading the file from Amazon S3: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool AuthenticateWithWindowsHello()
        {
            try
            {
                var result = KeyCredentialManager.RequestCreateAsync("SecLinkAppCredential", KeyCredentialCreationOption.ReplaceExisting).GetAwaiter().GetResult();
                return result.Status == KeyCredentialStatus.Success;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Windows Hello authentication failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private byte[] RetrieveSecureMasterKey()
        {
            try
            {
                string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SecLinkApp", "Keys", "masterKey.key");

                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException("Master key file not found.");
                }

                byte[] encryptedMasterKey = File.ReadAllBytes(filePath);
                byte[] decryptedMasterKeyBytes = ProtectedData.Unprotect(
                    encryptedMasterKey,
                    null,
                    DataProtectionScope.CurrentUser
                );
                string decryptedMasterKeyString = Encoding.UTF8.GetString(decryptedMasterKeyBytes);
                byte[] masterKey = Convert.FromBase64String(decryptedMasterKeyString);
                return masterKey;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to retrieve the master key: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        private string GetSaveDirectoryPath()
        {
            var saveDirectoryDialog = new System.Windows.Forms.FolderBrowserDialog();

            return saveDirectoryDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? saveDirectoryDialog.SelectedPath : null;
        }

        private void DecryptFile(byte[] encryptedContent, string outputFile, byte[] key, byte[] iv, DateTimeOffset timestamp)
        {
            try
            {
                EphemeralKeyGenerator keyGenerator = new EphemeralKeyGenerator(key);
                byte[] ephemeralKey = keyGenerator.GenerateKey(timestamp);

                // Debugging Information
                Console.WriteLine($"Ephemeral Key: {Convert.ToBase64String(ephemeralKey)}");

                FileCryptoManager.DecryptFileDirect(encryptedContent, outputFile, ephemeralKey, iv);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Decryption failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
