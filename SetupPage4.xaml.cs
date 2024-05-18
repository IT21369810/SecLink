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
                    if (result.Status == KeyCredentialStatus.Success)
                    {
                        MessageBox.Show("Setup complete! You're now ready to securely share files within your network. Click 'Finish' to start using SecureLink!");
                        finish.Visibility = Visibility.Visible;

                        byte[] masterKey = GenerateSecureMasterKey();
                        string masterKeyBase64 = Convert.ToBase64String(masterKey);

                        // Store the key securely
                        SecureStoreMasterKey(masterKey);
                                                
                        EphemeralKeyGenerator keyGenerator = new EphemeralKeyGenerator(masterKey);
                        byte[] ephemeralKey = keyGenerator.GenerateKey(DateTimeOffset.UtcNow);
                        // Use ephemeralKey for encryption as required
                        Console.WriteLine(masterKeyBase64);
                    }

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

            // Re-enable the Test button after the message box is acknowledged
            testButton.IsEnabled = true;
        }
        private byte[] GenerateSecureMasterKey()
        {
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                byte[] randomBytes = new byte[32]; // For GCM, we need a 256-bit key
                rng.GetBytes(randomBytes); // Fills the array with cryptographically secure random bytes
                return randomBytes;
            }
        }

        private void SecureStoreMasterKey(byte[] masterKey)
        {
            // Convert the key to a string (Base64) to store it
            string masterKeyString = Convert.ToBase64String(masterKey);

            // Use the ProtectedData class to encrypt and store the master key securely
            byte[] masterKeyEncrypted = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(masterKeyString),
                null, // Optional entropy
                DataProtectionScope.CurrentUser // Encrypt the data using the current user scope
            );

            // Store the encrypted key somewhere safe, e.g., a file or registry
            // This example uses a file in the user's AppData folder
            string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "masterKey.dat");
            File.WriteAllBytes(filePath, masterKeyEncrypted);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // Set the setup completed flag to true
            SecLinkApp.Properties.Settings.Default.SetupCompleted = true;
            SecLinkApp.Properties.Settings.Default.Save();
            // Open the main user interface window and close the setup window
            NavWindow mainWindow = new NavWindow();
            mainWindow.Left = this.Left; // 'this' refers to the current instance of the window
            mainWindow.Top = this.Top;
            mainWindow.Show();
            this.Close();
        }


        private void BackButton_Click4(object sender, RoutedEventArgs e)
        {
            SetupPage3 setupPage3 = new SetupPage3();
            setupPage3.Left = this.Left; // 'this' refers to the current instance of the window
            setupPage3.Top = this.Top;
            setupPage3.Show();
            this.Close();
        }
    }
}
