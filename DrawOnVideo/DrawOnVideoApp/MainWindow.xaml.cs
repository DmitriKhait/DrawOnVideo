using Emgu.CV;
using Emgu.CV.Structure;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DrawOnVideoApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        VideoCapture _capture;
        VideoWriter _videoWriter = null;
        bool _recording = false, _closing = false;
        int _frameWidth = 640, _frameHeight = 480;
        Mat _backGroundMat = new Mat();
        Bitmap _backGroundBitmap = null;
        string _outputDirectory = @"c:\temp";
        string _outputFileName = "video.mp4";
        string _outputScreenshotFile = "screenshot_{0}.jpg";
        bool _saveScreenShot = false;

        Bitmap overlay = null, resultFrame = null, videoFrame = null;

        public MainWindow()
        {
            InitializeComponent();

            OpenWebCam();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _closing = true;

            //cleanup
            if (overlay != null) overlay.Dispose();
            if (resultFrame != null) resultFrame.Dispose();
            if (videoFrame != null) videoFrame.Dispose();
            if (_capture != null) _capture.Dispose();
            if (_videoWriter != null) _videoWriter.Dispose();
            if (_backGroundMat != null) _backGroundMat.Dispose();
            if (_backGroundBitmap != null) _backGroundBitmap.Dispose();

            base.OnClosing(e);
        }

        private void OpenWebCam()
        {
            _capture = new VideoCapture(0, VideoCapture.API.DShow); // TODO: opens up a default webcam with default settings
            _capture.ImageGrabbed += ProcessFrame;

            if (_capture != null)
            {
                try
                {
                    _capture.Start();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Failed to open default webcam");
                    Debug.WriteLine($"{ex.Message}");
                }
            }
        }

        private void ProcessFrame(object sender, EventArgs e)
        {
            if (_capture != null && _capture.Ptr != IntPtr.Zero)
            {
                _capture.Retrieve(_backGroundMat, 0);

                _backGroundBitmap = _backGroundMat.ToBitmap();

                if (Application.Current != null && !_closing)
                    Application.Current.Dispatcher.Invoke(new Action(() => { imgPreview.Source = ImageSourceFromBitmap(_backGroundBitmap); }));


                if (_recording || _saveScreenShot)
                {
                    Application.Current.Dispatcher.Invoke(new Action(() => { overlay = RenderTargetToBitmap(inkCanvas); }));

                    resultFrame = Overlay(_backGroundBitmap, overlay);

                    if (_saveScreenShot)
                    {
                        resultFrame.Save(string.Format(System.IO.Path.Combine(_outputDirectory, _outputScreenshotFile), DateTime.Now.ToString("yyyyMMdd_hhmmss")));
                        _saveScreenShot = false;
                    }

                    if (_recording)
                        _videoWriter.Write(resultFrame.ToImage<Bgra, byte>().Mat);
                }
            }
        }

        private ImageSource ImageSourceFromBitmap(System.Drawing.Bitmap bmp)
        {
            if (!_closing)
            {
                var handle = bmp.GetHbitmap();
                try
                {
                    return Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                }
                finally { DeleteObject(handle); }
            }

            return null;
        }

        private Bitmap Overlay(Bitmap target, Bitmap overlay)
        {
            using (Graphics gra = Graphics.FromImage(target))
            {
                gra.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
                gra.DrawImage(overlay, new PointF(0, 0));

                return target;
            }
        }

        private Bitmap RenderTargetToBitmap(Visual source)
        {
            RenderTargetBitmap bmpRen = new RenderTargetBitmap(_frameWidth, _frameHeight, 96, 96, PixelFormats.Default);
            bmpRen.Render(source);

            MemoryStream stream = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bmpRen));
            encoder.Save(stream);

            Bitmap bitmap = new Bitmap(stream);

            return bitmap;
        }

        private void rbtColor_Click(object sender, RoutedEventArgs e)
        {
            RadioButton rb = sender as RadioButton;
            var color = rb.Content.ToString();

            inkCanvas.DefaultDrawingAttributes.Color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color);
        }

        private void btnSaveFrame_Click(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(_outputDirectory)) Directory.CreateDirectory(_outputDirectory);
            _saveScreenShot = true;
        }

        private void btnclearInkCanvas_Click(object sender, RoutedEventArgs e)
        {
            inkCanvas.Strokes.Clear();
        }

        private void btnStopRecordVideo_Click(object sender, RoutedEventArgs e)
        {
            _recording = false;
            btnStartRecordVideo.IsEnabled = true;
            btnStopRecordVideo.IsEnabled = false;

            if (_videoWriter != null)
            {
                if (_videoWriter.IsOpened)
                {
                    _videoWriter.Dispose();
                }

            }
        }

        private void btnRecordVideo_Click(object sender, RoutedEventArgs e)
        {
            int fourcc = (int)_capture.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.FourCC);
            _frameWidth = (int)_capture.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameWidth);
            _frameHeight = (int)_capture.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameHeight);
            double fps = (int)_capture.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.Fps);

            if (!Directory.Exists(_outputDirectory)) Directory.CreateDirectory(_outputDirectory);
            _videoWriter = new VideoWriter(System.IO.Path.Combine(_outputDirectory, _outputFileName), fourcc, 25, new System.Drawing.Size(_frameWidth, _frameHeight), true); // TODO: hardcoded FPS

            btnStartRecordVideo.IsEnabled = false;
            btnStopRecordVideo.IsEnabled = true;
            _recording = true;
        }

        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject([In] IntPtr hObject);
    }
}
