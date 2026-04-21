using System;
using System.IO;

namespace MediaConfigTool.Services
{
    /// <summary>
    /// Centralizes all local application data paths for Narrin Studio.
    /// </summary>
    public static class AppPathsService
    {
        private static readonly string _baseFolder =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Narrin Studio");

        public static string AppDataRoot => _baseFolder;
        public static string AssetsFolder => Path.Combine(_baseFolder, "Assets");
        public static string LogsFolder => Path.Combine(_baseFolder, "Logs");
        public static string MapsFolder => Path.Combine(_baseFolder, "maps");

        /// <summary>
        /// Creates all required local folders if they do not already exist.
        /// Safe to call multiple times.
        /// </summary>
        public static void EnsureFoldersExist()
        {
            Directory.CreateDirectory(AppDataRoot);
            Directory.CreateDirectory(AssetsFolder);
            Directory.CreateDirectory(LogsFolder);
            Directory.CreateDirectory(MapsFolder);
        }

        /// <summary>
        /// Returns the destination path for a map file.
        /// Uses a timestamp prefix to avoid name collisions.
        /// </summary>
        public static string GetMapDestinationPath(string sourceFileName)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var safeName = $"{timestamp}_{Path.GetFileName(sourceFileName)}";
            return Path.Combine(MapsFolder, safeName);
        }
    }
}