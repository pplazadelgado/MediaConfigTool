using MediaConfigTool.Models;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MediaConfigTool.Views
{
    public partial class PreviewWindow : Window
    {
        private readonly MediaRenderData _data;

        public PreviewWindow(MediaRenderData data)
        {
            InitializeComponent();
            _data = data;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Main image
            if (File.Exists(_data.FullPath))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(_data.FullPath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                MainImage.Source = bitmap;
            }

            // Map image
            if (!string.IsNullOrEmpty(_data.MapAssetUri) && File.Exists(_data.MapAssetUri))
            {
                var mapBitmap = new BitmapImage();
                mapBitmap.BeginInit();
                mapBitmap.UriSource = new Uri(_data.MapAssetUri);
                mapBitmap.CacheOption = BitmapCacheOption.OnLoad;
                mapBitmap.EndInit();
                MapImage.Source = mapBitmap;
            }

            // Metadata text
            DateText.Text = _data.CaptureTimestamp.HasValue
                ? _data.CaptureTimestamp.Value.ToString("dd MMMM yyyy")
                : string.Empty;

            LocationText.Text = _data.LocationName ?? string.Empty;

            EventText.Text = _data.EventNames.Count > 0
                ? string.Join(", ", _data.EventNames)
                : string.Empty;

            PersonText.Text = _data.PersonNames.Count > 0
                ? string.Join(", ", _data.PersonNames)
                : string.Empty;

            TagText.Text = _data.TagNames.Count > 0
                ? string.Join(", ", _data.TagNames)
                : string.Empty;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "PNG Image|*.png|JPEG Image|*.jpg",
                FileName = "render_output"
            };

            if (dialog.ShowDialog() != true) return;

            // Aseguramos que el canvas está renderizado
            RenderCanvas.Measure(new Size(1200, 1920));
            RenderCanvas.Arrange(new Rect(0, 0, 1200, 1920));
            RenderCanvas.UpdateLayout();

            var renderBitmap = new RenderTargetBitmap(
                1200, 1920, 96, 96, PixelFormats.Pbgra32);
            renderBitmap.Render(RenderCanvas);

            BitmapEncoder encoder = Path.GetExtension(dialog.FileName).ToLower() == ".jpg"
                ? new JpegBitmapEncoder()
                : new PngBitmapEncoder();

            encoder.Frames.Add(BitmapFrame.Create(renderBitmap));

            using var stream = File.OpenWrite(dialog.FileName);
            encoder.Save(stream);

            MessageBox.Show("Image saved successfully.", "Saved",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
