using SecureLink;
using System.Configuration;
using System.Data;
using System.Threading;
using System.Windows;

namespace SecLinkApp
{
    public partial class App : Application
    {
        public static WebSocketFileServer Server { get; private set; }
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DatabaseHelper.InitializeDatabase();
            String currentUsername = DatabaseHelper.GetUsername();
            Console.WriteLine("Application startup. Initializing server...");

            IProgress<DownloadProgressInfo> progressHandler = new Progress<DownloadProgressInfo>(progressInfo => {
                // Handle global progress updates here or simply ignore if using events for individual page updates.
            });

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

            Task.Run(async () =>
            {
                try
                {
                    Server = new WebSocketFileServer(port: 45679, progressHandler); // This line was corrected
                    await Server.StartAsync(_cancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Server startup was canceled.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred while starting the server: {ex.Message}");
                    Console.WriteLine(ex.StackTrace);
                }
            });
        }

            protected override void OnExit(ExitEventArgs e)
            {
            _cancellationTokenSource.Cancel();
            base.OnExit(e);
            }
    }
    
}
