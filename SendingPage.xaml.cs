using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Windows.Security.Credentials;

namespace SecLinkApp
{
    public partial class SendingPage : Page
    {
        public SendingPage()
        {
            InitializeComponent();
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "All files (*.*)|*.*",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                foreach (var fileName in openFileDialog.FileNames)
                {
                    SelectedFilesListBox.Items.Add(fileName);
                }
            }
        }

        private void RemoveFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                var itemToRemove = button.Tag;
                SelectedFilesListBox.Items.Remove(itemToRemove);
            }
        }

        private void SelectedFilesListBox_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (var file in files)
                {
                    SelectedFilesListBox.Items.Add(file);
                }
            }
        }

        private void SelectedFilesListBox_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedFilesListBox.Items.Count == 0)
            {
                MessageBox.Show("Please add files to upload.");
                return;
            }

            var sendButton = (Button)sender;
            sendButton.IsEnabled = false;

            try
            {
                bool isDoubleAuth = DoubleAuthRadioButton.IsChecked == true;

                if (isDoubleAuth)
                {
                    var faceAuthWindow = new FaceAuthenticationWindow();
                    bool? faceAuthResult = faceAuthWindow.ShowDialog();
                    if (faceAuthResult != true)
                    {
                        MessageBox.Show("Face authentication failed. Please try again.");
                        return;
                    }
                }

                bool helloAuthResult = await AuthenticateWithWindowsHelloAsync();
                if (!helloAuthResult)
                {
                    MessageBox.Show("Windows Hello authentication failed. Please try again.");
                    return;
                }

                List<string> encryptedFilePaths = new List<string>();
                byte[] masterKey = RetrieveSecureMasterKey();
                string defaultDirec = DatabaseHelper.GetDefaultDirectory(); // Retrieve the default directory

                Console.WriteLine($"Using Default Directory: {defaultDirec}");

                foreach (var filePath in SelectedFilesListBox.Items.Cast<string>())
                {
                    string fileName = Path.GetFileName(filePath);
                    string encryptedFilePath = Path.Combine(defaultDirec, $"{fileName}.enc"); // Use the default directory

                    // Ensure the directory exists
                    string directoryPath = Path.GetDirectoryName(encryptedFilePath);
                    if (!Directory.Exists(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }

                    byte[] iv;
                    byte[] ephemeralKey;
                    string timestamp;
                    EncryptFile(filePath, encryptedFilePath, masterKey, out iv, out ephemeralKey, out timestamp);
                    encryptedFilePaths.Add(encryptedFilePath);

                    string fileHash = CryptoUtils.GenerateHash(encryptedFilePath);
                    DatabaseHelper.SaveFileMetadata(fileHash, encryptedFilePath, Convert.ToBase64String(iv), isDoubleAuth ? "Double" : "Single", timestamp);

                    Console.WriteLine($"Ephemeral Key: {Convert.ToBase64String(ephemeralKey)}");
                    Console.WriteLine($"File Hash: {fileHash}");
                    Console.WriteLine($"Encrypted File Path: {encryptedFilePath}");
                    Console.WriteLine($"IV: {Convert.ToBase64String(iv)}");

                    HashValueTextBox.Text = $"File uploaded successfully with hash: {fileHash}";
                    HashValueTextBox.Visibility = Visibility.Visible;

                    // Upload the encrypted file to Amazon S3
                    string s3Key = Path.GetFileName(encryptedFilePath); // Use the file name as the S3 key
                    await FileCryptoManager.UploadFileToS3Async(encryptedFilePath, s3Key);
                    Console.WriteLine($"File uploaded successfully to S3 with key: {s3Key}");
                }

                MessageBox.Show("Files uploaded successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while uploading the files: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                sendButton.IsEnabled = true;
            }
        }



        private async Task<bool> AuthenticateWithWindowsHelloAsync()
        {
            bool isHelloSupported = await KeyCredentialManager.IsSupportedAsync();
            if (!isHelloSupported)
            {
                MessageBox.Show("Windows Hello is not supported on this device.", "Authentication Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            KeyCredentialRetrievalResult result = await KeyCredentialManager.RequestCreateAsync("SecLinkAppCredential", KeyCredentialCreationOption.ReplaceExisting);

            if (result.Status == KeyCredentialStatus.Success)
            {
                // Authentication is successful
                return true;
            }
            else
            {
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

        private void EncryptFile(string inputFile, string outputFile, byte[] key, out byte[] iv, out byte[] ephemeralKey, out string timestamp)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            timestamp = now.ToString("o"); // Use a round-trip format to preserve the timestamp
            EphemeralKeyGenerator keyGenerator = new EphemeralKeyGenerator(key);
            ephemeralKey = keyGenerator.GenerateKey(now);

            FileCryptoManager.EncryptFile(inputFile, outputFile, ephemeralKey, out iv);
        }
    }
}
