using Emgu.CV;
using Grimlok.Models;
using Grimlok.Services;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Grimlok
{
    public partial class MainWindow : Window
    {
        private readonly MonitorService _monitorService;

        public MainWindow(MonitorService monitorService)
        {
            InitializeComponent();
            _monitorService = monitorService;

            // Subscribe to status and frame events
            _monitorService.StatusChanged += OnStatusChanged;

            // Assume your MonitorService exposes an event like FrameCaptured(byte[] jpegBytes)
             _monitorService.FrameProcessed += OnFrameProcessed; 

            // Wire UI controls to service methods
            SliderThreshold.ValueChanged += (s, e) =>
                _monitorService.UpdateMotionThreshold((int)SliderThreshold.Value);

            ChkYolo.Checked += (s, e) => _monitorService.UpdateObjectDetectionEnabled(true);
            ChkYolo.Unchecked += (s, e) => _monitorService.UpdateObjectDetectionEnabled(false);

            ChkHumanOnly.Checked += (s, e) => _monitorService.UpdateHumanConfirmedOnly(true);
            ChkHumanOnly.Unchecked += (s, e) => _monitorService.UpdateHumanConfirmedOnly(false);

            // Set initial status
            OnStatusChanged(this, _monitorService.GetStatus());
        }

        // Thread-safe JPEG Byte to BitmapImage conversion
        private void OnFrameProcessed(object? sender, Mat mat)
        {
            if (mat == null || mat.IsEmpty) return;
            byte[] jpegBytes = mat.ToImage<Emgu.CV.Structure.Bgr, byte>().ToJpegData();

            Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    using var stream = new MemoryStream(jpegBytes);
                    var bitmap = new BitmapImage();

                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad; // Forces memory load immediately
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();

                    bitmap.Freeze(); // Crucial: Detaches the object from the thread, making it cross-thread safe

                    ImgFeed.Source = bitmap;

                    // Hide the inactive text once frames arrive
                    if (TxtFeedStatus.Visibility == Visibility.Visible)
                        TxtFeedStatus.Visibility = Visibility.Collapsed;
                }
                catch (Exception ex)
                {
                    // Log frame render failure if necessary
                    Console.WriteLine($"Render Error: {ex.Message}");
                }
            });
        }

        private void OnStatusChanged(object? sender, MonitorStatus status)
        {
            Dispatcher.Invoke(() =>
            {
                TxtStatus.Text = $"STATUS: {status.State.ToUpperInvariant()}";

                if (status.State != "Running")
                {
                    TxtFeedStatus.Visibility = Visibility.Visible;
                    ImgFeed.Source = null; // Clear image when stopped
                }

                TxtFeedStatus.Text = status.State switch
                {
                    "Running" => "[ MONITORING ACTIVE - CAPTURING FRAMES ]",
                    "Stopped" => "[ CAMERA STREAM INACTIVE - PRESS START MONITOR ]",
                    "Faulted" => $"[ ERROR: {status.LastError} ]",
                    _ => $"[ {status.State} ]"
                };

                ProgressActivity.Value = status.MotionEvents > 0 ? 50 : 0;
            });
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _monitorService.StartMonitoringAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start monitoring: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _monitorService.StopMonitoringAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to stop monitoring: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _monitorService.StatusChanged -= OnStatusChanged;
             _monitorService.FrameProcessed -= OnFrameProcessed;
            base.OnClosed(e);
        }
    }
}