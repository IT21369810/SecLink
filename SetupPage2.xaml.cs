using Microsoft.Win32;
using SecLinkApp;
using System.Windows;
using System;
using System.IO;
using System.Windows.Controls;
using Microsoft.VisualBasic.FileIO;

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

        private void TextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {

        }
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
            var dialog = new OpenFolderDialog
            {
                DefaultDirectory = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments),
                ValidateNames = false,
            };

            // Open the dialog and get the selected folder path
            if (dialog.ShowDialog() == true)
            {
                Browser_Box.Text = dialog.FolderName;
            }
        }
        private void BackButton_Click2(object sender, RoutedEventArgs e)
        {
            // Navigate back to SetupPage2.
            SetupPage1 SetupPage1 = new SetupPage1();
            SetupPage1.Left = this.Left; 
            SetupPage1.Top = this.Top;
            SetupPage1.Show();
            this.Close();
        }
        


    }

}
