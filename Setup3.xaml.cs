using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Face;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Emgu.CV.Util;
using SecureLink;

namespace SecLinkApp
{
    public partial class Setup3 : Window
    {
        private VideoCapture _capture;
        private CascadeClassifier _face;
        private List<Mat> _trainingImages = new List<Mat>();
        private List<string> _labels = new List<string>();
        private int _numLabels;
        private EigenFaceRecognizer _recognizer;
        private DispatcherTimer _timer;

        public Setup3()
        {
            InitializeComponent();
            InitializeCapture();
            LoadTrainingData();
            AuthenticateButton.Visibility = Visibility.Hidden;
        }

        private void InitializeCapture()
        {
            try
            {
                _capture = new VideoCapture();
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
                _timer = new DispatcherTimer();
                _timer.Tick += Timer_Tick;
                _timer.Interval = TimeSpan.FromMilliseconds(30);
                _timer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (_capture == null)
                {
                    throw new Exception("Capture device is not initialized.");
                }

                using (var frame = _capture.QueryFrame())
                {
                    if (frame == null)
                    {
                        throw new Exception("No frame captured from camera.");
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
                        StatusTextBlock.Text = label;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Runtime Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(NameTextBox.Text))
            {
                MessageBox.Show("Please enter a name.");
                return;
            }

            // Check if the entered username matches the stored username
            string storedUsername = DatabaseHelper.GetUsername();
            if (!string.IsNullOrEmpty(storedUsername) && !storedUsername.Equals(NameTextBox.Text, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Username is different", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Save the username to the database
            DatabaseHelper.SaveUserSettings(NameTextBox.Text, "DefaultDirectoryPath");

            try
            {
                using (var frame = _capture.QueryFrame().ToImage<Gray, byte>())
                {
                    var facesDetected = _face.DetectMultiScale(frame, 1.1, 10, new System.Drawing.Size(20, 20));

                    if (facesDetected.Length > 0)
                    {
                        var face = facesDetected[0];
                        var trainingFace = frame.Copy(face).Resize(100, 100, Inter.Cubic).Mat;
                        _trainingImages.Add(trainingFace);
                        _labels.Add(NameTextBox.Text);
                        SaveTrainingData();
                        TrainRecognizer();
                        StatusTextBlock.Text = "Face registered successfully!";
                        RegisterButton.IsEnabled = false; // Disable the Register button
                        AuthenticateButton.Visibility = Visibility.Visible; // Show the Authenticate button
                    }
                    else
                    {
                        StatusTextBlock.Text = "No face detected!";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Registration Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AuthenticateButton_Click(object sender, RoutedEventArgs e)
        {
            if (_recognizer == null)
            {
                MessageBox.Show("No training data found.");
                return;
            }

            try
            {
                using (var frame = _capture.QueryFrame().ToImage<Gray, byte>())
                {
                    var facesDetected = _face.DetectMultiScale(frame, 1.1, 10, new System.Drawing.Size(20, 20));

                    if (facesDetected.Length > 0)
                    {
                        var face = facesDetected[0];
                        var recognizedFace = frame.Copy(face).Resize(100, 100, Inter.Cubic).Mat;
                        var result = _recognizer.Predict(recognizedFace);
                        var label = result.Label == -1 ? "Unknown" : _labels[result.Label];

                        if (label == NameTextBox.Text)
                        {
                            StatusTextBlock.Text = "Authentication successful!";
                            AuthenticateButton.IsEnabled = false; // Disable the Authenticate button after successful authentication

                            // Redirect to SetupPage4
                            SetupPage4 setupPage4 = new SetupPage4();
                            setupPage4.Left = this.Left;
                            setupPage4.Top = this.Top;
                            setupPage4.Show();
                            this.Close();
                        }
                        else
                        {
                            StatusTextBlock.Text = "Authentication failed!";
                        }
                    }
                    else
                    {
                        StatusTextBlock.Text = "No face detected!";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Authentication Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadTrainingData()
        {
            if (File.Exists("TrainedLabels.txt"))
            {
                var labelsInfo = File.ReadAllText("TrainedLabels.txt").Split('%');
                _numLabels = Convert.ToInt32(labelsInfo[0]);

                for (int i = 1; i <= _numLabels; i++)
                {
                    _trainingImages.Add(CvInvoke.Imread($"face{i}.bmp", ImreadModes.Grayscale));
                    _labels.Add(labelsInfo[i]);
                }

                TrainRecognizer();
            }
        }

        private void SaveTrainingData()
        {
            File.WriteAllText("TrainedLabels.txt", $"{_trainingImages.Count}%");

            for (int i = 0; i < _trainingImages.Count; i++)
            {
                _trainingImages[i].Save($"face{i + 1}.bmp");
                File.AppendAllText("TrainedLabels.txt", $"{_labels[i]}%");
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

        private void BackButton_new_Click(object sender, RoutedEventArgs e)
        {
            // Stop the timer and release the camera
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Tick -= Timer_Tick;
            }

            if (_capture != null)
            {
                _capture.Dispose();
                _capture = null;
            }

            // Navigate to SetupPage2
            SetupPage2 setupPage2 = new SetupPage2();
            setupPage2.Left = this.Left;
            setupPage2.Top = this.Top;
            setupPage2.Show();
            this.Close();
        }
    }
}
