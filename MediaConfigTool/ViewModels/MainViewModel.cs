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
        public ObservableCollection<Location> Locations { get; } = new();
        public  ObservableCollection<Person> Persons { get; } = new();
        public ObservableCollection<Event> Events { get; } = new();
        public ObservableCollection<Tag> Tags { get; } = new();

        public RenderSettings RenderSettings { get; } = new();


        public int SelectedCount => SelectedMediaFiles.Count;
        public bool HasSelection => SelectedMediaFiles.Count > 0;
        public bool IsSingleSelection => SelectedMediaFiles.Count == 1;
        public bool IsMultiSelection => SelectedMediaFiles.Count > 1;
        public MediaFile? SelectedFile => IsSingleSelection ? SelectedMediaFiles[0] : null;
        public int SelectedImportedCount => SelectedMediaFiles.Count(f => f.IsImported);
        public int SelectedNotImportedCount => SelectedMediaFiles.Count(f => !f.IsImported);

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
            set
            {
                _selectedTenant = value;
                OnPropertyChanged();
                Locations.Clear();
                MediaFiles.Clear();
                Persons.Clear();
                Events.Clear();
                Tags.Clear();
                if (_selectedTenant is not null)
                    _ = LoadTenantDataAsync(_selectedTenant.TenantId);
            }
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

        private Location? _selectedLocation;
        public Location? SelectedLocation
        {
            get => _selectedLocation;
            set { _selectedLocation = value; OnPropertyChanged(); }
        }

        private Person? _selectedPerson;
        public Person? SelectedPerson
        {
            get => _selectedPerson;
            set { _selectedPerson = value; OnPropertyChanged();}
        }

        private Event? _selectedEvent;
        public Event? SelectedEvent
        {
            get => _selectedEvent;
            set { _selectedEvent = value; OnPropertyChanged(); }
        }

        private Tag? _selectedTag;
        public Tag? SelectedTag
        {
            get => _selectedTag;
            set { _selectedTag = value; OnPropertyChanged(); }
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
        public ICommand AssignLocationCommand { get; }
        public ICommand AssignPersonCommand {  get; }
        public ICommand AssignEventCommand { get; }
        public ICommand AssignTagCommand { get; }
        public ICommand CreateLocationCommand { get; }
        public ICommand EditLocationCommand { get; }
        public ICommand DeleteLocationCommand {  get; }

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

            AssignLocationCommand = new RelayCommand(
                async _ => await AssignLocationsAsync());

            AssignPersonCommand = new RelayCommand(
                async _ => await AssignMetadataAsync(
                    "person",
                    SelectedPerson?.PersonId,
                    SelectedPerson?.DisplayName,
                    (assetId, metaId) => _supabaseService.AssingPersonAsync(assetId, metaId, SelectedTenant!.TenantId)));

            AssignEventCommand = new RelayCommand(
                async _ => await AssignMetadataAsync(
                    "event",
                    SelectedEvent?.EventId,
                    SelectedEvent?.EventName,
                    (assetId, metaId) => _supabaseService.AssingEventAsync(assetId, metaId, SelectedTenant!.TenantId)));

            AssignTagCommand = new RelayCommand(
                async _ => await AssignMetadataAsync(
                    "tag",
                    SelectedTag?.TagId,
                    SelectedTag?.TagName,
                    (assetId, metaId) => _supabaseService.AssingTagAsync(assetId, metaId, SelectedTenant!.TenantId)));

            CreateLocationCommand = new RelayCommand(
                async _ => await CreateLocationAsync());
            EditLocationCommand = new RelayCommand(
                async _ => await EditLocationAsync());

            DeleteLocationCommand = new RelayCommand(
                async _ => await DeleteLocationAsync());
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
            StatusMessage = "Starting import....";

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
            catch (Exception ex)
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
            foreach (var item in selectedItems)
            {
                if (item is MediaFile file)
                    SelectedMediaFiles.Add(file);
            }

            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(IsSingleSelection));
            OnPropertyChanged(nameof(IsMultiSelection));
            OnPropertyChanged(nameof(SelectedFile));
            OnPropertyChanged(nameof(SelectedImportedCount));
            OnPropertyChanged(nameof(SelectedNotImportedCount));
            OnPropertyChanged(nameof(SelectedImportedCount));
            OnPropertyChanged(nameof(SelectedNotImportedCount));
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
                {
                    if (imported.TryGetValue(file.RelativePath, out var assetId))
                    {
                        file.IsImported = true;
                        file.MediaAssetId = assetId;
                    }
                    else
                    {
                        file.IsImported = false;
                        file.MediaAssetId = null;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] CheckImportedStatusAsync – {ex.Message}");
            }
        }

        private async Task LoadTenantDataAsync(string tenantId)
        {
            StatusIsWarning = false;
            StatusMessage = "Loading tenant data...";

            await Task.WhenAll(
                LoadLocationsAsync(tenantId),
                LoadPersonsAsync(tenantId),
                LoadEventsAsync(tenantId),
                LoadTagsAsync(tenantId));

            StatusIsWarning = false;
            StatusMessage = $"{Locations.Count} location(s), {Persons.Count} person(s), {Events.Count} event(s), {Tags.Count} tag(s) loaded.";
        }

        private async Task LoadLocationsAsync(string tenantId)
        {
            try
            {
                var locations = await _supabaseService.GetLocationAsync(tenantId);
                Locations.Clear();
                foreach (var loc in locations)
                    Locations.Add(loc);
            }
            catch (Exception ex)
            {
                StatusIsWarning = true;
                StatusMessage = "Error loading locations.";
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] LoadLocationsAsync: {ex.Message}");
            }
        }

        private async Task AssignLocationsAsync()
        {
            if (SelectedLocation == null)
            {
                StatusIsWarning = true;
                StatusMessage = "Select a location first.";
                return;
            }

            var pool = SelectedMediaFiles.Any()
                ? SelectedMediaFiles
                : (IEnumerable<MediaFile>)MediaFiles;

            var targets = pool.Where(f => f.IsImported && f.MediaAssetId != null).ToList();
            if (targets.Count == 0)
            {
                StatusIsWarning = true;
                StatusMessage = "No imported images found.";
                return;
            }

            int assigned = 0;
            int skipped = 0;
            int failed = 0;
            string? lastError = null;

            StatusIsWarning = false;
            StatusMessage = $"Assigning location to {targets.Count} image(s)...";

            foreach (var file in targets)
            {
                try
                {
                    var exists = await _supabaseService.MediaLocationExistsAsync(
                        file.MediaAssetId!, SelectedLocation.LocationId);

                    if (exists)
                    {
                        skipped++;
                        continue;
                    }

                    var ok = await _supabaseService.InsertMediaLocationAsync(
                        file.MediaAssetId!, SelectedLocation.LocationId, SelectedTenant!.TenantId);

                    if (ok) assigned++;
                    else failed++;
                }
                catch (Exception ex)
                {
                    failed++;
                    lastError = ex.Message;
                    System.Diagnostics.Debug.WriteLine($"[MainViewModel] AssignLocationsAsync - {file.FileName}: {ex.Message}");
                }
            }

            StatusIsWarning = failed > 0;
            var parts = new List<string>();
            if (assigned > 0) parts.Add($"{assigned} assigned");
            if (skipped > 0) parts.Add($"{skipped} skipped");
            if (failed > 0) parts.Add($"{failed} failed");
            StatusMessage = $"Location '{SelectedLocation.LocationName}': {string.Join(", ", parts)}"
                + (lastError != null ? $" — {lastError}" : ".");
        }

        private async Task AssignMetadataAsync(
    string metadataType,
    string? metadataId,
    string? metadataName,
    Func<string, string, Task<bool>> assignFunc)
        {
            if (metadataId == null)
            {
                StatusIsWarning = true;
                StatusMessage = $"Select a {metadataType} first.";
                return;
            }

            var pool = SelectedMediaFiles.Any()
                ? SelectedMediaFiles
                : (IEnumerable<MediaFile>)MediaFiles;

            var targets = pool.Where(f => f.IsImported && f.MediaAssetId != null).ToList();
            if (targets.Count == 0)
            {
                StatusIsWarning = true;
                StatusMessage = "No imported images found.";
                return;
            }

            int assigned = 0;
            int failed = 0;

            StatusIsWarning = false;
            StatusMessage = $"Assigning {metadataType} to {targets.Count} image(s)...";

            foreach (var file in targets)
            {
                try
                {
                    var ok = await assignFunc(file.MediaAssetId!, metadataId);
                    if (ok) assigned++;
                    else failed++;
                }
                catch (Exception ex)
                {
                    failed++;
                    System.Diagnostics.Debug.WriteLine($"[MainViewModel] Assign{metadataType} - {file.FileName}: {ex.Message}");
                }
            }

            StatusIsWarning = failed > 0;
            var parts = new List<string>();
            if (assigned > 0) parts.Add($"{assigned} assigned");
            if (failed > 0) parts.Add($"{failed} failed");
            StatusMessage = $"{metadataType} '{metadataName}': {string.Join(", ", parts)}.";
        }

        private async Task LoadPersonsAsync(string tenantId)
        {
            try
            {
                var persons = await _supabaseService.GetPersonAsync(tenantId);
                Persons.Clear();
                foreach (var p in persons) Persons.Add(p);
            }
            catch(Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] LoadPersonsAsync: {ex.Message}");
            }
        }

        private async Task LoadEventsAsync(string tenantId)
        {
            try
            {
                var events = await _supabaseService.GetEventsAsync(tenantId);
                Events.Clear();
                foreach (var e in events) Events.Add(e);
            }
            catch(Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] LoadEventsAsync: {ex.Message}");
            }
        }

        private async Task LoadTagsAsync(string tenantId)
        {
            try
            {
                var tags = await _supabaseService.GetTagsAsync(tenantId);
                Tags.Clear();
                foreach (var t in tags) Tags.Add(t);
            }
            catch(Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] LoadTagsAsync: {ex.Message}");
            }
        }

        private async Task CreateLocationAsync()
        {
            if(SelectedTenant == null)
            {
                StatusIsWarning = true;
                StatusMessage = "Select a tenant first.";
                return;
            }

            var dialog = new Views.CreateLocationWindow();
            dialog.Owner = Application.Current.MainWindow;

            if (dialog.ShowDialog() != true) return;

            try
            {
                StatusIsWarning = false;
                StatusMessage = "Creating location...";

                var ok = await _supabaseService.CreateLocationAsync(
                    dialog.LocationName,
                    dialog.LocationType,
                    SelectedTenant.TenantId);

                if (ok)
                {
                    StatusMessage = $"Location '{dialog.LocationName}' created";
                    await LoadLocationsAsync(SelectedTenant.TenantId);
                }
                else
                {
                    StatusIsWarning = true;
                    StatusMessage = "Failed to create location.";
                }
            }
            catch (Exception ex)
            {
                StatusIsWarning = true;
                StatusMessage = $"Error creating location: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] CreateLocationAsync: {ex.Message}");
            }
        }

        private async Task EditLocationAsync()
        {
            if (SelectedTenant == null) return;

            if(SelectedLocation == null)
            {
                StatusIsWarning = true;
                StatusMessage = "Select a location first,";
                return;
            }

            var dialog = new Views.CreateLocationWindow(SelectedLocation);
            dialog.Owner = Application.Current.MainWindow;

            if(dialog.ShowDialog() != true) return;

            try
            {
                StatusIsWarning = false;
                StatusMessage = "Updating location...";

                var ok = await _supabaseService.UpdateLocationAsync(
                    SelectedLocation.LocationId,
                    dialog.LocationName,
                    dialog.LocationType);

                if (ok)
                {
                    StatusMessage = $"Locacion '{dialog.LocationName}' updated";
                    await LoadLocationsAsync(SelectedTenant.TenantId);
                }
                else
                {
                    StatusIsWarning = true;
                    StatusMessage = "Failed to update location.";
                }
            }
            catch(Exception ex)
            {
                StatusIsWarning = true;
                StatusMessage = $"Error updating location: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] EditLocationAsync: {ex.Message}");
            }
        }

        private async Task DeleteLocationAsync()
        {
            if (SelectedTenant == null) return;

            if(SelectedLocation == null)
            {
                StatusIsWarning = true;
                StatusMessage = "Select a location first.";
                return;
            }

            var dialog = new Views.ConfirmDialog(
                $"Delete '{SelectedLocation.LocationName}'?");
                        dialog.Owner = Application.Current.MainWindow;

            if (dialog.ShowDialog() != true) return;

            try
            {
                StatusIsWarning = false;
                StatusMessage = "Deleting location...";

                var ok = await _supabaseService.DeleteLocationAsync(SelectedLocation.LocationId);

                if (ok)
                {
                    StatusMessage = "Location deleted.";
                    await LoadLocationsAsync(SelectedTenant.TenantId);
                }
                else
                {
                    StatusIsWarning = true;
                    StatusMessage = "Failed to delete location.";
                }
            }
            catch (Exception ex)
            {
                StatusIsWarning = true;
                StatusMessage = $"Error deleting location: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] DeleteLocationAsync: {ex.Message}");
            }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}