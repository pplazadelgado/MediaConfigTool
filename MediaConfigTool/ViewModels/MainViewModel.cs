using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MediaConfigTool.Models;
using MediaConfigTool.Services;
using System.Collections;

namespace MediaConfigTool.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly SupabaseService _supabaseService;
        private readonly MediaFileService _mediaFileService;
        private readonly ImportService _importService;

        private CancellationTokenSource? _importCts;

        public ObservableCollection<Tenant> Tenants { get; } = new();
        public ObservableCollection<MediaFile> MediaFiles { get; } = new();
        public ObservableCollection<FolderItem> RootFolders { get; } = new();
        public ObservableCollection<MediaFile> SelectedMediaFiles { get; } = new();

        public int SelectedCount => SelectedMediaFiles.Count;
        public bool HasSelection => SelectedMediaFiles.Count > 0;
        public bool IsSingleSelection => SelectedMediaFiles.Count == 1;
        public bool IsMultiSelection => SelectedMediaFiles.Count > 1;
        public MediaFile? SelectedFile => IsSingleSelection ? SelectedMediaFiles[0]: null;

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

        private bool _isImporting;
        public bool IsImporting
        {
            get => _isImporting;
            set { _isImporting = value; OnPropertyChanged(); }
        }


        private bool _isFolderPanelOpen = false;
        public GridLength FolderPanelWidth =>
            _isFolderPanelOpen ? new GridLength(260) : new GridLength(0);
        public Visibility FolderPanelVisibility =>
            _isFolderPanelOpen ? Visibility.Visible : Visibility.Collapsed;

        public ICommand BrowseFolderCommand { get; }
        public ICommand CloseFolderPanelCommand { get; }
        public ICommand ImportCommand { get; }
        public ICommand CancelImportCommand { get; }

        public MainViewModel()
        {
            _supabaseService = new SupabaseService();
            _mediaFileService = new MediaFileService();
            _importService = new ImportService(_supabaseService);

            BrowseFolderCommand = new RelayCommand(_ => BrowseFolder());
            CloseFolderPanelCommand = new RelayCommand(_ => CloseFolderPanel());

            ImportCommand = new RelayCommand(
                 async _ => await StartImportAsync(),
                 _ => !IsImporting && SelectedTenant != null && HasSelection);

            CancelImportCommand = new RelayCommand(
                _ => _importCts?.Cancel(),
                _ => IsImporting);

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

                var files = _mediaFileService.GetMediaFiles(folderPath, folderPath);

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

                StatusMessage = $"{MediaFiles.Count} images loaded. Checking import status...";
                await CheckImportedStatusAsync();
                StatusMessage = $"{MediaFiles.Count} images loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Error loading images.";
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] LoadMediaFilesAsync: {ex.Message}");
            }
        }

        private async Task StartImportAsync()
        {
            if (SelectedTenant == null) return;

            IsImporting = true;
            StatusMessage = "Starting import ....";

            _importCts = new CancellationTokenSource();

            var progress = new Progress<string>(message =>
            {
                StatusMessage = message;
            });

            try
            {
                var result = await _importService.ImportAsync(
                    SelectedMediaFiles,
                    SelectedTenant.TenantId,
                    SelectedFolder,
                    progress,
                    _importCts.Token);

                StatusMessage = result.Summary;
                await CheckImportedStatusAsync();

                if (result.HasErrors)
                {
                    foreach (var error in result.Errors)
                        System.Diagnostics.Debug.WriteLine($"[Import] {error}");
                }
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Import cancelled";
            }
            catch(Exception ex)
            {
                StatusMessage = $"Import failed: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] ImportAsync error - {ex.Message}");
            }
            finally
            {
                IsImporting = false;
                _importCts?.Dispose();
                _importCts = null;
            }
        }

        public void OnSelectionChanged(IList selectedItems)
        {
            SelectedMediaFiles.Clear();
            foreach(var item in selectedItems)
            {
                if(item is MediaFile file)
                    SelectedMediaFiles.Add(file);
            }

            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(IsSingleSelection));
            OnPropertyChanged(nameof(IsMultiSelection));
            OnPropertyChanged(nameof(SelectedFile));
        }

        private async Task CheckImportedStatusAsync()
        {
            if (SelectedTenant == null || MediaFiles.Count == 0) return;

            try
            {
                var relativePaths = MediaFiles.Select(f => f.RelativePath);
                var imported = await _supabaseService.GetImportedRelativePathsAsync(
                    SelectedTenant.TenantId, relativePaths);

                foreach (var file in MediaFiles)
                    file.IsImported = imported.Contains(file.RelativePath);
            }
            catch(Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] CheckImportedStatusAsync – {ex.Message}");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}