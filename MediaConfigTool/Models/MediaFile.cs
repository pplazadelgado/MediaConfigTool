

using System.IO.Packaging;
using System.Windows.Media.Imaging;

namespace MediaConfigTool.Models
{
    public class MediaFile
    {
        public string FileName { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public DateTime? CaptureTimestamp { get; set; }
        public int Orientation { get; set; }
        public BitmapImage? Thumbnail { get; set; }
    }
}
