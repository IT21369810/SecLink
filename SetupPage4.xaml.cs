using SecureLink;
using System.Windows;
using System.Windows.Controls;
using Windows.Security.Credentials;
using System.Security.Cryptography;
using System.IO;
using System.Text;

namespace SecLinkApp
{
    public partial class SetupPage4 : Window
    {

        public SetupPage4()
        {
            InitializeComponent();
        }

        private async void Test_Click(object sender, RoutedEventArgs e)
        {
            // Disable the Test button to prevent multiple clicks
            var testButton = (Button)sender;
            testButton.IsEnabled = false;

            bool supported = await KeyCredentialManager.IsSupportedAsync();
            if (supported)
            {
                KeyCredentialRetrievalResult result =
                    await KeyCredentialManager.RequestCreateAsync("login",
                    KeyCredentialCreationOption.ReplaceExisting);

                if (result.Status == KeyCredentialStatus.Success)
                {
                    MessageBox.Show("Setup complete! You're now ready to securely Store files!!");
                    finish.Visibility = Visibility.Visible;

                    byte[] masterKey = GenerateSecureMasterKey();
                    string masterKeyBase64 = Convert.ToBase64String(masterKey);

                    // Store the key securely
                    SecureStoreMasterKey(masterKey);

                    Console.WriteLine(masterKeyBase64);
                }
                else
                {
                    MessageBox.Show("Login failed. Try again");
                }
            }
            else
            {
                MessageBox.Show("Windows Hello is not supported on this device.");
            }

            testButton.IsEnabled = true;
        }

        private byte[] GenerateSecureMasterKey()
        {
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                byte[] randomBytes = new byte[32]; //  256-bit key
                rng.GetBytes(randomBytes); //IV
                return randomBytes;
            }
        }

        private void SecureStoreMasterKey(byte[] masterKey)
        {
            try
            {
                string masterKeyString = Convert.ToBase64String(masterKey);

                byte[] masterKeyEncrypted = ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(masterKeyString),
                    null,
                    DataProtectionScope.CurrentUser
                );

                string folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SecLinkApp", "Keys");
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                string filePath = Path.Combine(folderPath, "masterKey.key");
                File.WriteAllBytes(filePath, masterKeyEncrypted);
                Console.WriteLine($"Master key securely stored at: {filePath}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to store the master key: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            SecLinkApp.Properties.Settings.Default.SetupCompleted = true;
            SecLinkApp.Properties.Settings.Default.Save();

            NavWindow mainWindow = new NavWindow();
            mainWindow.Left = this.Left;
            mainWindow.Top = this.Top;
            mainWindow.Show();
            this.Close();
        }

        private void BackButton_Click4(object sender, RoutedEventArgs e)
        {
            Setup3 setup3 = new Setup3();
            setup3.Left = this.Left;
            setup3.Top = this.Top;
            setup3.Show();
            this.Close();
        }
    }
}
