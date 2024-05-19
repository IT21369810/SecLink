using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SecLinkApp
{
    
    public partial class NavWindow : Window
    {
        public NavWindow()
        {
            InitializeComponent();
            Main.Content = new SendingPage();
        }

        private void Button_Click_Send(object sender, RoutedEventArgs e)
        {
            // Redirect to SendingPage
            Main.Content = new SendingPage();
        }
        private void Button_Click_Receive(object sender, RoutedEventArgs e)
        {
            // Redirect to ReceivingPage
            Main.Content = new ReceivingPage();
        }
        private void Button_Click_Settings(object sender, RoutedEventArgs e)
        {
            // Redirect to SettingsPage
            //Main.Content = new SettingsPage();
        }

        
        private void Send_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Redirect to SendingPage
            Main.Content = new SendingPage();
        }
        private void Receive_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Redirect to ReceivingPage
            Main.Content = new ReceivingPage();
        }
        private void Settings_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Redirect to SettingsPage
            //Main.Content = new SettingsPage();
        }

    }
}
