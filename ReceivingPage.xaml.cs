using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Documents;
using System.Windows.Input;

namespace SecLinkApp
{
    public partial class ReceivingPage : Page
    {
        private Dictionary<string, (ProgressBar progressBar, TextBlock fileName, TextBlock fileSize, TextBlock timeRemaining)> progressElements = new Dictionary<string, (ProgressBar, TextBlock, TextBlock, TextBlock)>();

        public ReceivingPage()
        {
            InitializeComponent();
            Loaded += ReceivingPage_Loaded;
        }

        public void UpdateDownloadProgress(DownloadProgressInfo progressInfo)
        {
            Dispatcher.Invoke(() =>
            {

                if (!progressElements.TryGetValue(progressInfo.FileName, out var elements))
                {
                    elements = CreateProgressUI(progressInfo.FileName);
                    progressElements[progressInfo.FileName] = elements;
                    elements.progressBar.Tag = DateTime.Now; // Tag the start time to the progressBar
                }

                double transferredMB = progressInfo.BytesReceived / 1024.0 / 1024.0;
                double totalMB = progressInfo.TotalBytes / 1024.0 / 1024.0;
                elements.progressBar.Value = progressInfo.Percentage;
                elements.fileName.Text = progressInfo.FileName;
                elements.fileSize.Text = $"Transferred {transferredMB:0.00} MB of {totalMB:0.00} MB";


                // Calculate the speed and estimate time remaining
                DateTime startTime = (DateTime)elements.progressBar.Tag;
                TimeSpan timeElapsed = DateTime.Now - startTime;
                double speed = progressInfo.BytesReceived / timeElapsed.TotalSeconds; // bytes per second
                double timeRemainingSeconds = (progressInfo.TotalBytes - progressInfo.BytesReceived) / speed;
                TimeSpan estimatedTimeRemaining = TimeSpan.FromSeconds(timeRemainingSeconds);

                elements.timeRemaining.Text = $"Time remaining: {estimatedTimeRemaining.ToString(@"hh\:mm\:ss")}";
            });
        }


        private void ReceivingPage_Loaded(object sender, RoutedEventArgs e)
        {
            var server = App.Server; // Assuming this accesses WebSocketFileServer instance
            server.ProgressChanged += UpdateDownloadProgress;
            server.StatusMessageUpdated += (s, e) => AppendStatusMessage(e.Message, e.Color);
        }

        private (ProgressBar progressBar, TextBlock fileName, TextBlock fileSize, TextBlock timeRemaining) CreateProgressUI(string fileName)
        {
            var progressBar = new ProgressBar { Minimum = 0, Maximum = 100, Height = 20, Margin = new Thickness(5) };
            var fileNameTextBlock = new TextBlock { Text = fileName, Margin = new Thickness(5) };
            var fileSizeTextBlock = new TextBlock { Margin = new Thickness(5) };
            var timeRemainingTextBlock = new TextBlock { Margin = new Thickness(5) };

            var stopButton = new Button
            {
                Content = "Stop",
                Foreground = new SolidColorBrush(Colors.Red),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF580000")),
                Margin = new Thickness(5),
                Tag = fileName, // Use the safe file name as the tag
                Cursor = Cursors.Hand,

            };


            // Add the new UI elements to the dynamic stack panel
            DynamicProgressStackPanel.Children.Add(progressBar);
            DynamicProgressStackPanel.Children.Add(fileNameTextBlock);
            DynamicProgressStackPanel.Children.Add(fileSizeTextBlock);
            DynamicProgressStackPanel.Children.Add(timeRemainingTextBlock);
            DynamicProgressStackPanel.Children.Add(stopButton);


            return (progressBar, fileNameTextBlock, fileSizeTextBlock, timeRemainingTextBlock);
        }

        private void AppendStatusMessage(string message, Brush color)
        {
            // Ensure we're on the UI thread
            Dispatcher.Invoke(() =>
            {
                if (color == null)
                {
                    color = Brushes.Black; // Default color
                }

                // Create a new Run for the message
                Run messageRun = new Run(message)
                {
                    Foreground = color
                };

                Paragraph paragraph = new Paragraph();
                paragraph.Inlines.Add(new Run($"{message}\n") { Foreground = color });
                StatusMessagesRichTextBox.Document.Blocks.Add(paragraph);

               
                // Scroll to the bottom whenever a new message is added
                StatusMessagesRichTextBox.ScrollToEnd();
            });
        }
    }
}