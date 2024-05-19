using Microsoft.Win32;
using SecLinkApp;
using System.Windows;
using System;

namespace SecureLink
{
    public partial class SetupPage2 : Window
    {
        public SetupPage2()
        {
            InitializeComponent();
            var username = DatabaseHelper.GetUsername();
            var defaultDirectory = DatabaseHelper.GetDefaultDirectory();
        }

        private void TextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) { }

        private void NextButton_Click2(object sender, RoutedEventArgs e)
        {
            var folderPath = Browser_Box.Text.Trim();

            if (!string.IsNullOrEmpty(folderPath))
            {
                // Retrieve the username from the database
                var username = DatabaseHelper.GetUsername();

                // Check if the username was successfully retrieved
                if (!string.IsNullOrEmpty(username))
                {

                    // Save the directory path with the username
                    DatabaseHelper.SaveUserSettings(username, folderPath);
                    Console.WriteLine($"Using Default Directory: {folderPath}");

                    // Navigate to SetupPage3 or the main window
                    Setup3 setup3 = new Setup3();
                    setup3.Left = this.Left;
                    setup3.Top = this.Top;
                    setup3.Show();
                    this.Close();
                }
                else
                {
                    errorTextBlockBrowse.Visibility = Visibility.Visible;
                }
            }
            else
            {
                errorTextBlockBrowse.Visibility = Visibility.Visible;
            }
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments)
            };

            // Open the dialog and get the selected folder path
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Browser_Box.Text = dialog.SelectedPath;
            }
        }

        private void BackButton_Click2(object sender, RoutedEventArgs e)
        {
            // Navigate back to SetupPage1.
            SetupPage1 setupPage1 = new SetupPage1();
            setupPage1.Left = this.Left;
            setupPage1.Top = this.Top;
            setupPage1.Show();
            this.Close();
        }
    }
}
