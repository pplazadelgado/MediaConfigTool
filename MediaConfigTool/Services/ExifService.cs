using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace MediaConfigTool.Services
{
    public class ExifService
    {
        ///<summary>
        ///Reds capture timestamp and orientation from the EXIF metadata of an image file.
        ///If Exif data is not available, it returns null for the timestamp and 1 for the orientation (default).
        ///</summary>
        public (DateTime? CaptureTimestamp, int orientation)ReadMetadata(string fullPath)
        {
            try
            {
                using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);

                var frame = BitmapFrame.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                var metadata = frame.Metadata as BitmapMetadata;

                if (metadata == null)
                    return (FallbackTimestamp(fullPath), 1);

                var timestamp = ParseTimestamp(metadata);
                var orientation = ParseOrientation(metadata);

                return (timestamp ?? FallbackTimestamp(fullPath), orientation);
            }
            catch(Exception)
            {
                return (FallbackTimestamp(fullPath), 1);
            }
        }

        // ── Private helpers ────────────────────────────────────────────────
        private DateTime? ParseTimestamp(BitmapMetadata metadata)
        {
            try
            {
                // EXIF date is stored as string: "yyyy:MM:dd HH:mm:ss"
                var raw = metadata.DateTaken;
                if (!string.IsNullOrWhiteSpace(raw) &&
                    DateTime.TryParseExact(raw, "yyyy:MM:dd HH:mm:ss",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None,
                        out var parsed))
                {
                    return parsed;
                }
            }
            catch(Exception)
            {
            }

            return null;
        }

        private int ParseOrientation(BitmapMetadata metadata)
        {
            try
            {
                // EXIF orientation is stored at query path //app1/ifd/ushort/{ushort=274}
                var raw = metadata.GetQuery("/app1/ifd/{ushort=274}");
                if (raw is ushort value && value >= 1 && value <= 8)
                    return value;
            }
            catch(Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ExifServie] ParseOrientation failed - {ex.Message}");
            }

            return 1;
        }

        private DateTime FallbackTimestamp(string fullPath)
        {
            try
            {
                return File.GetLastWriteTime(fullPath);
            }
            catch
            {
                return DateTime.UtcNow;
            }
        }
    }
}
