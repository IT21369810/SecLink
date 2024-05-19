using SecureLink;
using System.Configuration;
using System.Data;
using System.Threading;
using System.Windows;

namespace SecLinkApp
{
    public partial class App : Application
    {
        
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DatabaseHelper.InitializeDatabase();
            String currentUsername = DatabaseHelper.GetUsername();
            Console.WriteLine("Application startup. Initializing server...");


            if (currentUsername != null)
            {
                // Show the main application window
                var mainWindow = new NavWindow();
                mainWindow.Show();
            }
            else
            {
                // Start with the first setup page
                var setupPage = new SetupPage1();
                setupPage.Show();
            }

        }

            protected override void OnExit(ExitEventArgs e)
            {
            _cancellationTokenSource.Cancel();
            base.OnExit(e);
            }
    }
    
}
