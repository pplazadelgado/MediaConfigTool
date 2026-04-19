using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace MediaConfigTool.Models
{
    public class MediaFile : INotifyPropertyChanged
    {
        public string FileName { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public DateTime? CaptureTimestamp { get; set; }
        public int Orientation { get; set; } = 1;
        public BitmapImage? Thumbnail { get; set; }
        public string? MediaAssetId {  get; set; }

        private bool _isImported;
        public bool IsImported
        {
            get => _isImported;
            set { _isImported = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}