using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Face;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Linq;
using Emgu.CV.Util;

namespace SecLinkApp
{
    public partial class FaceAuthenticationWindow : Window
    {
        private VideoCapture _capture;
        private CascadeClassifier _face;
        private EigenFaceRecognizer _recognizer;
        private DispatcherTimer _frameTimer;
        private DispatcherTimer _countdownTimer;
        private List<Mat> _trainingImages = new List<Mat>();
        private List<string> _labels = new List<string>();
        private int _remainingTime;

        public string AuthenticatedUsername { get; private set; }

        public FaceAuthenticationWindow()
        {
            InitializeComponent();
            InitializeCapture();
            LoadTrainingData();
            StartCountdownTimer();
        }

        private void InitializeCapture()
        {
            try
            {
                _capture = new VideoCapture(0); // Using default API
                if (!_capture.IsOpened)
                {
                    throw new Exception("Camera could not be opened.");
                }

                string cascadeFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "haarcascade_frontalface_default.xml");
                if (!File.Exists(cascadeFilePath))
                {
                    throw new FileNotFoundException($"File '{cascadeFilePath}' not found.");
                }

                _face = new CascadeClassifier(cascadeFilePath);
                _frameTimer = new DispatcherTimer();
                _frameTimer.Tick += FrameTimer_Tick;
                _frameTimer.Interval = TimeSpan.FromMilliseconds(30);
                _frameTimer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void StartCountdownTimer()
        {
            _remainingTime = 30; // 30 seconds countdown
            _countdownTimer = new DispatcherTimer();
            _countdownTimer.Tick += CountdownTimer_Tick;
            _countdownTimer.Interval = TimeSpan.FromSeconds(1);
            _countdownTimer.Start();
        }

        private void CountdownTimer_Tick(object sender, EventArgs e)
        {
            _remainingTime--;
            StatusTextBlock.Text = $"Time remaining: {_remainingTime} seconds";

            if (_remainingTime <= 0)
            {
                _countdownTimer.Stop();
                _frameTimer.Stop();
                _capture.Dispose();
                MessageBox.Show("Timer expired. Please try again.", "Timeout", MessageBoxButton.OK, MessageBoxImage.Warning);
                this.DialogResult = false;
                this.Close();
            }
        }

        private void FrameTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (_capture == null || !_capture.IsOpened)
                {
                    throw new Exception("Capture device is not initialized or opened.");
                }

                using (var frame = _capture.QueryFrame())
                {
                    if (frame == null)
                    {
                        StatusTextBlock.Text = "No frame captured from the camera.";
                        return;
                    }

                    var image = frame.ToImage<Bgr, byte>();
                    var grayFrame = image.Convert<Gray, byte>();
                    var facesDetected = _face.DetectMultiScale(grayFrame, 1.1, 10, new System.Drawing.Size(20, 20));

                    foreach (var face in facesDetected)
                    {
                        image.Draw(face, new Bgr(System.Drawing.Color.Red), 2);
                    }

                    CameraFeed.Source = ToBitmapSource(image.Mat);

                    if (_recognizer != null && facesDetected.Length > 0)
                    {
                        var recognizedFace = grayFrame.Copy(facesDetected[0]).Resize(100, 100, Inter.Cubic).Mat;
                        var result = _recognizer.Predict(recognizedFace);
                        var label = result.Label == -1 ? "Unknown" : _labels[result.Label];

                        if (label != "Unknown")
                        {
                            AuthenticatedUsername = label;
                            StatusTextBlock.Text = "Authentication successful!";
                            _countdownTimer.Stop();
                            _frameTimer.Stop();
                            _capture.Dispose();
                            MessageBox.Show("Authentication successful!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                            this.DialogResult = true;
                            return;
                        }
                        else
                        {
                            StatusTextBlock.Text = "Authentication failed!";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Error: {ex.Message}";
            }
        }


        //load model
        private void LoadTrainingData()
        {
            if (File.Exists("TrainedLabels.txt"))
            {
                var labelsInfo = File.ReadAllText("TrainedLabels.txt").Split('%');
                int numLabels = Convert.ToInt32(labelsInfo[0]);

                for (int i = 1; i <= numLabels; i++)
                {
                    _trainingImages.Add(CvInvoke.Imread($"face{i}.bmp", ImreadModes.Grayscale));
                    _labels.Add(labelsInfo[i]);
                }

                TrainRecognizer();
            }
        }

        private void TrainRecognizer()
        {
            _recognizer = new EigenFaceRecognizer(_trainingImages.Count, double.PositiveInfinity);
            var images = new VectorOfMat();
            var labels = new VectorOfInt();
            for (int i = 0; i < _trainingImages.Count; i++)
            {
                images.Push(_trainingImages[i]);
                labels.Push(new int[] { i });
            }
            _recognizer.Train(images, labels);
        }

        private BitmapSource ToBitmapSource(Mat mat)
        {
            using (var source = mat.ToBitmap())
            {
                var hBitmap = source.GetHbitmap();
                var bs = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                DeleteObject(hBitmap);
                return bs;
            }
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr hObject);
    }
}
