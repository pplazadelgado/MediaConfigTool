using MediaConfigTool.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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
                    // Read orientation from EXIF before loading thumbnail
                    int orientation = 1;
                    try
                    {
                        using var metaStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                        var frame = BitmapFrame.Create(metaStream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                        var metadata = frame.Metadata as BitmapMetadata;
                        if (metadata != null)
                        {
                            var raw = metadata.GetQuery("/app1/ifd/{ushort=274}");
                            if (raw is ushort value && value >= 1 && value <= 8)
                                orientation = value;
                        }
                    }
                    catch { /* orientation stays 1 */ }

                    // Load the bitmap
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(fullPath);
                    bitmap.DecodePixelWidth = decodeWidth;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    // Apply rotation if needed
                    double angle = orientation switch
                    {
                        3 => 180,
                        6 => 90,
                        8 => 270,
                        _ => 0
                    };

                    if (angle == 0)
                        return (BitmapImage?)bitmap;

                    var transformed = new TransformedBitmap(bitmap, new RotateTransform(angle));
                    transformed.Freeze();

                    // Convert back to BitmapImage for consistency
                    var encoder = new JpegBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(transformed));

                    using var ms = new System.IO.MemoryStream();
                    encoder.Save(ms);
                    ms.Position = 0;

                    var rotated = new BitmapImage();
                    rotated.BeginInit();
                    rotated.StreamSource = ms;
                    rotated.CacheOption = BitmapCacheOption.OnLoad;
                    rotated.EndInit();
                    rotated.Freeze();

                    return (BitmapImage?)rotated;
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