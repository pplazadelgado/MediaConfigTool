using System;
using System.IO;

namespace MediaConfigTool.Services
{
    ///<summary>
    ///Centralizes all local application data paths.
    ///Currently scaffoled for future phases - not actively used yn Phase 2
    /// </summary>
    public class AppPathsService
    {
        private const string AppFolderName = "MediaConfigTool";

        ///<summary>
        ///Root folder for all local app data.
        /// Example: C:\Users\John\AppData\Local\MediaConfigTool
        /// </summary>
        public string AppDataRoot { get; }

        /// <summary>
        /// Folder for cached map snapshots and other visual assets.
        /// Example: C:\Users\John\AppData\Local\MediaConfigTool\Assets
        /// </summary>
        public string AssetsFolder {  get; }

        /// <summary>
        /// Folder for local log files.
        /// Example: C:\Users\John\AppData\Local\MediaConfigTool\Logs
        /// </summary>
        public string LogsFolder {  get; }

        public AppPathsService() 
        {
            AppDataRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppFolderName);

            AssetsFolder = Path.Combine(AppDataRoot, "Assets");
            LogsFolder = Path.Combine(AppDataRoot, "Logs");
        }

        /// <summary>
        /// Creates all required local folders if they do not already exist.
        /// Safe to call multiple times.
        /// </summary>
        public void EnsureFolderExist()
        {
            Directory.CreateDirectory(AppDataRoot);
            Directory.CreateDirectory(AssetsFolder);
            Directory.CreateDirectory(LogsFolder);
        }
    }
}
