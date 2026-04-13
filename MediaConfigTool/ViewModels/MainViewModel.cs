using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MediaConfigTool.Models;
using MediaConfigTool.Services;

namespace MediaConfigTool.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly SupabaseService _supabaseService;
        private readonly MediaFileService _mediaFileService;

        public ObservableCollection<Tenant> Tenants { get; } = new();
        public ObservableCollection<MediaFile> MediaFiles { get; } = new();
        public ObservableCollection<FolderItem> RootFolders { get; } = new();

        private bool _statusIsWarning = false;
        public bool StatusIsWarning
        {
            get => _statusIsWarning;
            set { _statusIsWarning = value; OnPropertyChanged(); }
        }

        private bool _tenantsLoaded = false;
        public bool TenantsLoaded
        {
            get => _tenantsLoaded;
            set { _tenantsLoaded = value; OnPropertyChanged(); }
        }

        private Tenant? _selectedTenant;
        public Tenant? SelectedTenant
        {
            get => _selectedTenant;
            set { _selectedTenant = value; OnPropertyChanged(); }
        }

        private string _statusMessage = "Ready";
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        private string _selectedFolder = string.Empty;
        public string SelectedFolder
        {
            get => _selectedFolder;
            set { _selectedFolder = value; OnPropertyChanged(); }
        }

        private bool _isFolderPanelOpen = false;
        public GridLength FolderPanelWidth =>
            _isFolderPanelOpen ? new GridLength(260) : new GridLength(0);
        public Visibility FolderPanelVisibility =>
            _isFolderPanelOpen ? Visibility.Visible : Visibility.Collapsed;

        public ICommand BrowseFolderCommand { get; }
        public ICommand CloseFolderPanelCommand { get; }

        public MainViewModel()
        {
            _supabaseService = new SupabaseService();
            _mediaFileService = new MediaFileService();

            BrowseFolderCommand = new RelayCommand(BrowseFolder);
            CloseFolderPanelCommand = new RelayCommand(CloseFolderPanel);
        }

        public async Task LoadTenantsAsync()
        {
            try
            {
                StatusIsWarning = false;
                StatusMessage = "Connecting to Supabase...";
                var tenants = await _supabaseService.GetTenantsAsync();
                Tenants.Clear();
                foreach (var t in tenants)
                    Tenants.Add(t);

                if (Tenants.Count > 0)
                {
                    TenantsLoaded = true;
                    StatusIsWarning = false;
                    StatusMessage = $"{Tenants.Count} tenant(s) loaded.";
                }
                else
                {
                    TenantsLoaded = false;
                    StatusIsWarning = true;
                    StatusMessage = "No tenants found. The tenant table may be empty.";
                }
            }
            catch (Exception ex)
            {
                TenantsLoaded = false;
                StatusIsWarning = true;
                StatusMessage = "Could not connect to Supabase. Check your API key or network.";
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] {ex.Message}");
            }
        }

        private void BrowseFolder()
        {
            if (_isFolderPanelOpen)
            {
                CloseFolderPanel();
                return;
            }

            RootFolders.Clear();
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
                RootFolders.Add(new FolderItem(drive.RootDirectory.FullName));

            _isFolderPanelOpen = true;
            OnPropertyChanged(nameof(FolderPanelWidth));
            OnPropertyChanged(nameof(FolderPanelVisibility));
        }

        private void CloseFolderPanel()
        {
            _isFolderPanelOpen = false;
            OnPropertyChanged(nameof(FolderPanelWidth));
            OnPropertyChanged(nameof(FolderPanelVisibility));
        }

        public async Task SelectFolderAsync(string folderPath)
        {
            SelectedFolder = folderPath;
            await LoadMediaFilesAsync(folderPath);
        }

        private async Task LoadMediaFilesAsync(string folderPath)
        {
            try
            {
                MediaFiles.Clear();
                StatusMessage = "Scanning folder...";

                var files = _mediaFileService.GetMediaFiles(folderPath);

                if (files.Count == 0)
                {
                    StatusIsWarning = true;
                    StatusMessage = "No images found in selected folder.";
                    return;
                }

                StatusIsWarning = false;
                StatusMessage = $"{files.Count} images found. Loading previews...";

                foreach (var file in files)
                {
                    file.Thumbnail = await _mediaFileService.LoadThumbnailAsync(file.FullPath);
                    if (file.Thumbnail is not null)
                        MediaFiles.Add(file);
                }

                StatusMessage = $"{MediaFiles.Count} images loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Error loading images.";
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] LoadMediaFilesAsync: {ex.Message}");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}