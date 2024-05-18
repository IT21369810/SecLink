using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace SecLinkApp
{
    public partial class AcceptDeclineWindow : Window
    {
        public bool Accepted { get; private set; } = false;

        public AcceptDeclineWindow(string senderName, List<string> receivedFilesList, long totalFileSize)
        {
            InitializeComponent();

            SenderNameTextBlock.Text = senderName;
            foreach (var fileName in receivedFilesList)
            {
                FilesListBox.Items.Add(fileName);
            }

            // Set total file size
            TotalFileSizeTextBlock.Text = $"{totalFileSize / (1024 * 1024.0):N2} MB"; // Convert bytes to MB and format
        }

        private async void AcceptButton_Click(object sender, RoutedEventArgs e)
        {
            var acceptButton = (Button)sender;
            acceptButton.IsEnabled = false;

            // Perform facial recognition authentication
            var faceAuthWindow = new FaceAuthenticationWindow();
            bool? authResult = faceAuthWindow.ShowDialog();

            if (authResult == true)
            {
                // Check if the authenticated username matches the stored username in the database
                string authenticatedUsername = faceAuthWindow.AuthenticatedUsername;
                string storedUsername = DatabaseHelper.GetUsername();
                if (authenticatedUsername.Equals(storedUsername, System.StringComparison.OrdinalIgnoreCase))
                {
                    Accepted = true;
                    MessageBox.Show("Authentication successful.");
                }
                else
                {
                    MessageBox.Show("Authentication failed. Username does not match.");
                }
            }
            else
            {
                MessageBox.Show("Authentication failed or cancelled.");
            }

            acceptButton.IsEnabled = true;
            Close();
        }

        private void DeclineButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
