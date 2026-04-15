using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using MediaConfigTool.Models;

namespace MediaConfigTool.Services
{
    public class MediaFileService
    {
        private static readonly string[] SupportedExtensions = { ".jpg", ".jpeg", ".png" };
        private readonly ExifService _exifService = new();

        public List<MediaFile> GetMediaFiles(string folderPath, string rootPath)
        {
            var result = new List<MediaFile>();

            try
            {
                foreach (var file in Directory.EnumerateFiles(folderPath))
                {
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    if (Array.Exists(SupportedExtensions, e => e == ext))
                    {
                        var (timestamp, orientation) = _exifService.ReadMetadata(file);

                        result.Add(new MediaFile
                        {
                            FileName = Path.GetFileName(file),
                            FullPath = file,
                            RelativePath = Path.GetRelativePath(rootPath, file),
                            CaptureTimestamp = timestamp,
                            Orientation = orientation
                        });
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                System.Diagnostics.Debug.WriteLine($"[MediaFileService] Access denied: {folderPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MediaFileService] {ex.Message}");
            }

            return result;
        }

        public Task<BitmapImage?> LoadThumbnailAsync(string fullPath, int decodeWidth = 200)
        {
            return Task.Run(() =>
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(fullPath);
                    bitmap.DecodePixelWidth = decodeWidth;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return (BitmapImage?)bitmap;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MediaFileService] thumbnail failed: {fullPath} – {ex.Message}");
                    return null;
                }
            });
        }
    }
}