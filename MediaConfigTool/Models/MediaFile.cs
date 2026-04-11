

using System.Windows.Media.Imaging;

namespace MediaConfigTool.Models
{
    public class MediaFile
    {
        public string FileName { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public BitmapImage? Thumbnail { get; set; }
    }
}
