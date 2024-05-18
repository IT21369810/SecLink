using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using System.Linq; 
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Security.Credentials;
using System.IO;
using System.Text;
using System.Windows.Media;
using System.Security.Cryptography;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Engines;




namespace SecLinkApp
{
    public partial class SendingPage : Page 
    {
        
        public SendingPage()
        {
            InitializeComponent();
            
        }

        private void Directory_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Archive files (*.zip;*.rar)|*.zip;*.rar", 
                Multiselect = true // Allow multiple file selections
            };

            if (openFileDialog.ShowDialog() == true)
            {
                // Display selected file names in the TargetListBox
                foreach (var fileName in openFileDialog.FileNames)
                {
                    TargetListBox.Items.Add(fileName);
                    Console.WriteLine($"Added file from directory: {fileName}");
                }
            }
        }
        private void RemoveFile_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button != null)
            {
                var itemToRemove = button.Tag;
                TargetListBox.Items.Remove(itemToRemove);
            }
        }

        private void ListBox_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                // Filter for .zip and .rar files and add them to the TargetListBox
                foreach (var file in files.Where(f => f.EndsWith(".zip") || f.EndsWith(".rar")))
                {
                    TargetListBox.Items.Add(file);
                    Console.WriteLine($"Added file from drag & drop: {file}");
                }
            }
        }
        private void ListBox_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Any(f => f.EndsWith(".zip") || f.EndsWith(".rar")))
                {
                    e.Effects = DragDropEffects.Copy; 
                }
            }

            e.Handled = true;
        }

        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            if (TargetListBox.Items.Count == 0)
            {
                MessageBox.Show("Please add files to upload.");
                return;
            }

            var sendButton = (Button)sender;
            sendButton.IsEnabled = false;

            // Open the face authentication window
            var faceAuthWindow = new FaceAuthenticationWindow();
            bool? authResult = faceAuthWindow.ShowDialog();

            if (authResult != true)
            {
                MessageBox.Show("Authentication failed. Please try again.");
                sendButton.IsEnabled = true;
                return;
            }

            // Retrieve authenticated username
            string authenticatedUsername = faceAuthWindow.AuthenticatedUsername;

            // Check if the authenticated username matches the stored username in the database
            string storedUsername = DatabaseHelper.GetUsername();
            if (!authenticatedUsername.Equals(storedUsername, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Authentication failed. Username does not match.");
                sendButton.IsEnabled = true;
                return;
            }

            // Clear the dynamic progress UI to ensure we are starting fresh
            DynamicProgressStackPanel.Children.Clear();
            UnregisterNameOfDynamicUIElements();

            var filePaths = TargetListBox.Items.Cast<string>().ToArray();
            var progressHandler = new Progress<UploadProgressInfo>(UpdateUIWithProgress);

            // Retrieve the master key
            byte[] masterKey = RetrieveSecureMasterKey();
            if (masterKey == null)
            {
                MessageBox.Show("Failed to retrieve the master key. Encryption cannot proceed.");
                sendButton.IsEnabled = true;
                return;
            }

            // Generate an ephemeral key
            EphemeralKeyGenerator keyGenerator = new EphemeralKeyGenerator(masterKey);
            byte[] ephemeralKey = keyGenerator.GenerateKey(DateTimeOffset.UtcNow);
            Console.Write(ephemeralKey);

            string ephemeralKeyBase64 = Convert.ToBase64String(ephemeralKey);
            Console.WriteLine($"Ephemeral Key (Base64): {ephemeralKeyBase64}");

            // Encrypt the files
            List<string> encryptedFilePaths = new List<string>();
            foreach (var filePath in TargetListBox.Items.Cast<string>())
            {
                string encryptedFilePath = $"{filePath}.enc";
                FileCryptoManager.EncryptFile(filePath, encryptedFilePath, ephemeralKey);
                encryptedFilePaths.Add(encryptedFilePath);

                
                CreateProgressUI(filePath, encryptedFilePath);
            }

            SideWindowSend window = new SideWindowSend(encryptedFilePaths.ToArray(), progressHandler, ephemeralKey);
            window.Closed += (s, args) => Dispatcher.Invoke(() => sendButton.IsEnabled = true);
            window.Show();

            sendButton.IsEnabled = true;
        }



        private void UnregisterNameOfDynamicUIElements()
        {
            foreach (var element in fileUploadUIElements.Values)
            {
                UnregisterName(element.progressBar.Name);
                UnregisterName(element.fileNameTextBlock.Name);
                UnregisterName(element.fileSizeTextBlock.Name);
                UnregisterName(element.timeRemainingTextBlock.Name);
            }
            fileUploadUIElements.Clear();
        }

        private void CreateProgressUI(string originalFilePath, string encryptedFilePath)
        {
            Console.WriteLine($"Creating progress UI for {encryptedFilePath}");
            string safeFileName = GetSafeFileNameForNameProperty(Path.GetFileName(encryptedFilePath));
            string displayFileName = Path.GetFileName(originalFilePath); // Display the original file name to the user
            string fileProgressName = $"{safeFileName}_ProgressBar";
            string fileNameBlockName = $"{safeFileName}_FileNameTextBlock";
            string fileSizeBlockName = $"{safeFileName}_FileSizeTextBlock";
            string timeRemainingBlockName = $"{safeFileName}_TimeRemainingTextBlock";
            string stopButtonName = $"{safeFileName}_StopButton";

            var progressBar = new ProgressBar
            {
                Name = fileProgressName,
                Height = 20,
                Margin = new Thickness(10),
                Maximum = 100,
                Value = 0
            };

            var fileNameTextBlock = new TextBlock
            {
                Name = fileNameBlockName,
                Margin = new Thickness(10, 5, 10, 0),
                Text = $"Uploading {displayFileName}"
            };

            var fileSizeTextBlock = new TextBlock
            {
                Name = fileSizeBlockName,
                Margin = new Thickness(10, 5, 10, 0),
                Text = "0 MB of ? MB"
            };

            var timeRemainingTextBlock = new TextBlock
            {
                Name = timeRemainingBlockName,
                Margin = new Thickness(10, 5, 10, 20),
                Text = "Time remaining: Calculating..."
            };

            var stopButton = new Button
            {
                Content = "Stop",
                Foreground = new SolidColorBrush(Colors.Red),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF580000")),
                Margin = new Thickness(5),
                Tag = safeFileName, // Use the safe file name as the tag
                Cursor = Cursors.Hand,

            };
            stopButton.Click += StopFileTransfer_Click;


            DynamicProgressStackPanel.Children.Add(progressBar);
            DynamicProgressStackPanel.Children.Add(fileNameTextBlock);
            DynamicProgressStackPanel.Children.Add(fileSizeTextBlock);
            DynamicProgressStackPanel.Children.Add(timeRemainingTextBlock);
            DynamicProgressStackPanel.Children.Add(stopButton);

            RegisterName(fileProgressName, progressBar);
            RegisterName(fileNameBlockName, fileNameTextBlock);
            RegisterName(fileSizeBlockName, fileSizeTextBlock);
            RegisterName(timeRemainingBlockName, timeRemainingTextBlock);
            RegisterName(stopButtonName, stopButton);

            fileUploadUIElements[safeFileName] = (progressBar, fileNameTextBlock, fileSizeTextBlock, timeRemainingTextBlock, stopButton);
        }

        private void StopFileTransfer_Click(object sender, RoutedEventArgs e)
        {
            var sendButton = (Button)sender;
            var stopButton = sender as Button;
            var safeFileName = stopButton.Tag.ToString();
            var stopButtonName = $"{safeFileName}_StopButton"; // This must match exactly what was used in RegisterName

            if (fileUploadUIElements.TryGetValue(safeFileName, out var uiElements))
            {
                DynamicProgressStackPanel.Children.Remove(uiElements.progressBar);
                DynamicProgressStackPanel.Children.Remove(uiElements.fileNameTextBlock);
                DynamicProgressStackPanel.Children.Remove(uiElements.fileSizeTextBlock);
                DynamicProgressStackPanel.Children.Remove(uiElements.timeRemainingTextBlock);
                DynamicProgressStackPanel.Children.Remove(uiElements.stopButton);

                UnregisterName(stopButtonName); // Correctly use the name that was registered

                fileUploadUIElements.Remove(safeFileName);

                if (AllUploadsStoppedOrCompleted())
                {

                    sendButton.IsEnabled = true;
                }
            }
        }
        private bool AllUploadsStoppedOrCompleted()
        {
            MessageBox.Show("File Transfer Stopped");
            return true;
        }

        private string GetSafeFileNameForNameProperty(string fileName)
        {
            var safeFileName = new StringBuilder();
            bool isFirstChar = true;

            foreach (char c in fileName)
            {
                // If the first character is a digit, prepend an underscore
                if (isFirstChar && char.IsDigit(c))
                {
                    safeFileName.Append('_');
                }
                isFirstChar = false;

                // Allow letters, digits, and underscores only
                if (char.IsLetterOrDigit(c) || c == '_')
                {
                    safeFileName.Append(c);
                }
                else
                {
                    safeFileName.Append('_');
                }
            }
            return safeFileName.ToString();
        }


        private Dictionary<string, (ProgressBar progressBar, TextBlock fileNameTextBlock, TextBlock fileSizeTextBlock, TextBlock timeRemainingTextBlock, Button stopButton)> fileUploadUIElements = new Dictionary<string, (ProgressBar, TextBlock, TextBlock, TextBlock, Button)>();

        private void UpdateUIWithProgress(UploadProgressInfo progressInfo)
        {
            Dispatcher.Invoke(() =>
            {
                string safeFileName = GetSafeFileNameForNameProperty(progressInfo.FileName);
                string fileProgressName = $"{safeFileName}_ProgressBar";
                string fileNameBlockName = $"{safeFileName}_FileNameTextBlock";
                string fileSizeBlockName = $"{safeFileName}_FileSizeTextBlock";
                string timeRemainingBlockName = $"{safeFileName}_TimeRemainingTextBlock";

                ProgressBar progressBar = FindName(fileProgressName) as ProgressBar;
                TextBlock fileNameTextBlock = FindName(fileNameBlockName) as TextBlock;
                TextBlock fileSizeTextBlock = FindName(fileSizeBlockName) as TextBlock;
                TextBlock timeRemainingTextBlock = FindName(timeRemainingBlockName) as TextBlock;

                Console.WriteLine($"Updating progress for {safeFileName}, {progressInfo.Percentage}% done.");

                if (progressBar != null) progressBar.Value = progressInfo.Percentage;
                if (fileNameTextBlock != null) fileNameTextBlock.Text = $"Uploading {safeFileName}";
                if (fileSizeTextBlock != null) fileSizeTextBlock.Text = $"{progressInfo.BytesSent / 1024.0 / 1024.0:N2} MB of {progressInfo.TotalBytes / 1024.0 / 1024.0:N2} MB";
                if (timeRemainingTextBlock != null) timeRemainingTextBlock.Text = $"Time remaining: {progressInfo.TimeRemaining}";
            });
        }
        private byte[] RetrieveSecureMasterKey()
        {
            // file in the user's AppData folder
            string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "masterKey.dat");

            try
            {
                // Read the encrypted master key from the file
                byte[] encryptedMasterKey = File.ReadAllBytes(filePath);

                // Use the ProtectedData class to decrypt the master key
                byte[] decryptedMasterKeyBytes = ProtectedData.Unprotect(
                    encryptedMasterKey,
                    null,
                    DataProtectionScope.CurrentUser // Decrypt the data using the current user scope
                );

                // Convert the decrypted key back from a string (Base64)
                string decryptedMasterKeyString = Encoding.UTF8.GetString(decryptedMasterKeyBytes);
                return Convert.FromBase64String(decryptedMasterKeyString);

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to retrieve the master key: {ex.Message}");
                return null;
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
        


    }
}