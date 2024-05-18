using SecLinkApp;
using System.Windows;
using System.Windows.Controls;

namespace SecureLink
{
    public partial class SetupPage1 : Window
    {
        public SetupPage1()
        {
            InitializeComponent();
         
        }
 
        private string currentUsername;
        private void NextButton_Click1(object sender, RoutedEventArgs e)
        {
            var username = usernameBox.Text.Trim();

            if (!string.IsNullOrWhiteSpace(username))
            {
                // Save the username
                DatabaseHelper.SaveUserSettings(username, null); // Use null or an empty string for the directory initially

                // Navigate to SetupPage2
                SetupPage2 setupPage2 = new SetupPage2();
                setupPage2.Left = this.Left;
                setupPage2.Top = this.Top;
                setupPage2.Show();
                this.Close();
            }
            else
            {
                errorTextBlock.Visibility = Visibility.Visible; 
            }
        }

        private void VideoBackground_MediaEnded(object sender, RoutedEventArgs e)
        {
            VideoBackground.Position = TimeSpan.Zero;
            VideoBackground.Play();
        }

        private void usernameBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            currentUsername = usernameBox.Text;
        }
        
    }
}
