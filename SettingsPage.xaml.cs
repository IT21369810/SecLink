using Microsoft.Win32;
using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using Windows.Security.Credentials;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;

namespace SecLinkApp
{
    public partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            InitializeComponent();

            // Set the text of the TextBox controls to the values retrieved from the database
            usernamebox.Text = DatabaseHelper.GetUsername();
            defaultlocatiqonbox.Text = DatabaseHelper.GetDefaultDirectory();
        }
        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            // Get the new username entered by the user
            var newUsername = usernamebox.Text;
            Console.Write(newUsername);

            var sendButton = (Button)sender;
            sendButton.IsEnabled = false;

            bool isAuthenticated = await AuthenticateWithWindowsHelloAsync();

            if (!isAuthenticated)
            {
                MessageBox.Show("Authentication failed. Please try again.");
                sendButton.IsEnabled = true;
                return;
            }

            // Update the username in the database
            DatabaseHelper.SaveUserSettings(newUsername, DatabaseHelper.GetDefaultDirectory()); 

            // username has been saved
            MessageBox.Show("Username updated successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

            sendButton.IsEnabled = true; // Re-enable the button after the operation is complete
        }


        // Implement the Click event handler for the Browse button
        private async void Browse_Click(object sender, RoutedEventArgs e)
        {
            var sendButton = (Button)sender;
            sendButton.IsEnabled = false;

            bool isAuthenticated = await AuthenticateWithWindowsHelloAsync();

            if (!isAuthenticated)
            {
                MessageBox.Show("Authentication failed. Please try again.");
                sendButton.IsEnabled = true;
                return;
            }

            var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog
             {
                Description = "Select a folder for file receiving:",
                UseDescriptionForTitle = true,
                SelectedPath = DatabaseHelper.GetDefaultDirectory() ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            if (dialog.ShowDialog() == true)
            {
                // Update the TextBox with the selected folder path
                defaultlocatiqonbox.Text = dialog.SelectedPath;

                // Update the default directory in the database
                DatabaseHelper.SaveUserSettings(DatabaseHelper.GetUsername(), dialog.SelectedPath);
            }
            sendButton.IsEnabled = true;
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
