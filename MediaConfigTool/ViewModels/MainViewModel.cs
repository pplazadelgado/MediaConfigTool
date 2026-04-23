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
using System.Text;

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
        public ObservableCollection<TagCategory> TagCategories { get; } = new();
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
                TagCategories.Clear();
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

        private MediaFile? _selectedMedia;
        public MediaFile? SelectedMedia
        {
            get => _selectedMedia;
            set
            {
                _selectedMedia = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanRender));
            }
        }

        public bool CanRender => SelectedMedia?.MediaAssetId != null;

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

        // Colecciones para selección múltiple de metadata (Phase 4)
        public ObservableCollection<Location> SelectedLocations { get; } = new();
        public ObservableCollection<Person> SelectedPersons { get; } = new();
        public ObservableCollection<Event> SelectedEvents { get; } = new();
        public ObservableCollection<Tag> SelectedTags { get; } = new();

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
        public ICommand CreatePersonCommand { get; }
        public ICommand EditPersonCommand { get; }
        public ICommand DeletePersonCommand { get; }
        public ICommand CreateEventCommand { get; }
        public ICommand EditEventCommand { get; }
        public ICommand DeleteEventCommand { get; }
        public ICommand CreateTagCommand { get; }
        public ICommand CreateTagCategoryCommand { get; }
        public ICommand EditTagCommand { get; }
        public ICommand DeleteTagCommand { get; }
        public ICommand LoadMapCommand { get; }
        public ICommand AssignMapCommand { get; }
        public ICommand RenderImageCommand { get; }

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
        SelectedPersons.Select(p => p.PersonId),
        null,
        (assetId, metaId) => _supabaseService.AssingPersonAsync(assetId, metaId, SelectedTenant!.TenantId),
        (assetId, metaId) => _supabaseService.MediaPersonEsixtsAsync(assetId, metaId)));

            AssignEventCommand = new RelayCommand(
                async _ => await AssignMetadataAsync(
                    "event",
                    SelectedEvents.Select(e => e.EventId),
                    null,
                    (assetId, metaId) => _supabaseService.AssingEventAsync(assetId, metaId, SelectedTenant!.TenantId),
                    (assetId, metaId) => _supabaseService.MediaEventExistsAsync(assetId, metaId)));

            AssignTagCommand = new RelayCommand(
                async _ => await AssignMetadataAsync(
                    "tag",
                    SelectedTags.Select(t => t.TagId),
                    null,
                    (assetId, metaId) => _supabaseService.AssingTagAsync(assetId, metaId, SelectedTenant!.TenantId),
                    (assetId, metaId) => _supabaseService.MediaTagExistsAsync(assetId, metaId)));

            CreateLocationCommand = new RelayCommand(
                async _ => await CreateLocationAsync());
            EditLocationCommand = new RelayCommand(
                async _ => await EditLocationAsync());

            DeleteLocationCommand = new RelayCommand(
                async _ => await DeleteLocationAsync());

            CreatePersonCommand = new RelayCommand(
                async _ => await CreatePersonAsync());
            EditPersonCommand = new RelayCommand(
                async _ => await EditPersonAsync());
            DeletePersonCommand = new RelayCommand(
                async _ => await DeletePersonAsync());

            CreateEventCommand = new RelayCommand(
                async _ => await CreateEventAsync());
            EditEventCommand = new RelayCommand(
                async _ => await EditEventAsync());
            DeleteEventCommand = new RelayCommand(
                async _ => await DeleteEventAsync());

            CreateTagCommand = new RelayCommand(
                async _ => await CreateTagAsync());
            CreateTagCategoryCommand = new RelayCommand(
                async _ => CreateTagCategoryAsync());
            EditTagCommand = new RelayCommand(
                async _ => await EditTagAsync());
            DeleteTagCommand = new RelayCommand(
                async _ => await DeleteTagAsync());
            LoadMapCommand = new RelayCommand(
                async _ => await LoadMapAsync());

            AssignMapCommand = new RelayCommand(
                async _ => await AssignMapAsync());
            RenderImageCommand = new RelayCommand(
                _ => RenderImage());
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

        public void OnLocationSelectionChanged(IList selectedItems)
        {
            SelectedLocations.Clear();
            foreach (var item in selectedItems)
                if (item is Location loc) SelectedLocations.Add(loc);

            // Mantiene SelectedLocation sincronizado para Edit/Delete
            SelectedLocation = SelectedLocations.FirstOrDefault();
        }

        public void OnPersonSelectionChanged(IList selectedItems)
        {
            SelectedPersons.Clear();
            foreach (var item in selectedItems)
                if (item is Person p) SelectedPersons.Add(p);

            SelectedPerson = SelectedPersons.FirstOrDefault();
        }

        public void OnEventSelectionChanged(IList selectedItems)
        {
            SelectedEvents.Clear();
            foreach (var item in selectedItems)
                if (item is Event e) SelectedEvents.Add(e);

            SelectedEvent = SelectedEvents.FirstOrDefault();
        }

        public void OnTagSelectionChanged(IList selectedItems)
        {
            SelectedTags.Clear();
            foreach (var item in selectedItems)
                if (item is Tag t) SelectedTags.Add(t);

            SelectedTag = SelectedTags.FirstOrDefault();
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
                LoadTagsAsync(tenantId),
                LoadTagCategoriesAsync(tenantId));

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

        // Determina qué imágenes son objetivo de una asignación según las reglas de Phase 4:
        // - Si hay imágenes seleccionadas: usa solo las importadas de esa selección
        // - Si no hay selección: usa todas las importadas visibles en la galería
        // - Devuelve null si no hay ningún objetivo válido (el caller muestra el mensaje)
        private List<MediaFile>? GetAssignmentTargets(out string? warningMessage)
        {
            warningMessage = null;

            bool hasSelection = SelectedMediaFiles.Any();

            if (hasSelection)
            {
                // Regla 1 y 5: hay selección → usar solo las importadas de esa selección
                var targets = SelectedMediaFiles
                    .Where(f => f.IsImported && f.MediaAssetId != null)
                    .ToList();

                if (targets.Count == 0)
                {
                    // Regla 3 aplicada a selección: ninguna de las seleccionadas está importada
                    warningMessage = "No imported images found in this folder. Please import images first.";
                    return null;
                }

                return targets;
            }
            else
            {
                // Regla 2: sin selección → usar todas las importadas visibles
                var targets = MediaFiles
                    .Where(f => f.IsImported && f.MediaAssetId != null)
                    .ToList();

                if (targets.Count == 0)
                {
                    // Regla 3: no hay ninguna importada visible en la carpeta
                    warningMessage = "No imported images found in this folder. Please import images first.";
                    return null;
                }

                return targets;
            }
        }


        private async Task AssignLocationsAsync()
        {
            if (!SelectedLocations.Any())
            {
                StatusIsWarning = true;
                StatusMessage = "Select a location first.";
                return;
            }

            var targets = GetAssignmentTargets(out var warning);
            if (targets == null)
            {
                StatusIsWarning = true;
                StatusMessage = warning!;
                return;
            }

            int assigned = 0;
            int skipped = 0;
            int failed = 0;
            string? lastError = null;

            StatusIsWarning = false;
            StatusMessage = $"Assigning {SelectedLocations.Count} location(s) to {targets.Count} image(s)...";

            foreach (var location in SelectedLocations)
            {
                foreach (var file in targets)
                {
                    try
                    {
                        var exists = await _supabaseService.MediaLocationExistsAsync(
                            file.MediaAssetId!, location.LocationId);

                        if (exists) { skipped++; continue; }

                        var ok = await _supabaseService.InsertMediaLocationAsync(
                            file.MediaAssetId!, location.LocationId, SelectedTenant!.TenantId);

                        if (ok) assigned++;
                        else failed++;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        lastError = ex.Message;
                        System.Diagnostics.Debug.WriteLine(
                            $"[MainViewModel] AssignLocationsAsync - {file.FileName}: {ex.Message}");
                    }
                }
            }

            StatusIsWarning = failed > 0;
            var parts = new List<string>();
            if (assigned > 0) parts.Add($"{assigned} assigned");
            if (skipped > 0) parts.Add($"{skipped} skipped");
            if (failed > 0) parts.Add($"{failed} failed");
            StatusMessage = $"Locations: {string.Join(", ", parts)}"
                + (lastError != null ? $" — {lastError}" : ".");
        }

        private async Task AssignMetadataAsync(
    string metadataType,
    IEnumerable<string> metadataIds,
    string? labelForStatus,
    Func<string, string, Task<bool>> assignFunc,
    Func<string, string, Task<bool>>? existsFunc = null)
        {
            if (!metadataIds.Any())
            {
                StatusIsWarning = true;
                StatusMessage = $"Select a {metadataType} first.";
                return;
            }

            var targets = GetAssignmentTargets(out var warning);
            if (targets == null)
            {
                StatusIsWarning = true;
                StatusMessage = warning!;
                return;
            }

            int assigned = 0;
            int skipped = 0;
            int failed = 0;
            string? lastError = null;

            StatusIsWarning = false;
            StatusMessage = $"Assigning {metadataType} to {targets.Count} image(s)...";

            foreach (var metadataId in metadataIds)
            {
                foreach (var file in targets)
                {
                    try
                    {
                        if (existsFunc != null)
                        {
                            var exists = await existsFunc(file.MediaAssetId!, metadataId);
                            if (exists) { skipped++; continue; }
                        }

                        var ok = await assignFunc(file.MediaAssetId!, metadataId);
                        if (ok) assigned++;
                        else failed++;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        lastError = ex.Message;
                        System.Diagnostics.Debug.WriteLine(
                            $"[MainViewModel] Assign{metadataType} - {file.FileName}: {ex.Message}");
                    }
                }
            }

            StatusIsWarning = failed > 0;
            var parts = new List<string>();
            if (assigned > 0) parts.Add($"{assigned} assigned");
            if (skipped > 0) parts.Add($"{skipped} skipped");
            if (failed > 0) parts.Add($"{failed} failed");
            StatusMessage = $"{metadataType}: {string.Join(", ", parts)}"
                + (lastError != null ? $" — {lastError}" : ".");
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

            if (SelectedLocation == null)
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
        private async Task CreatePersonAsync()
        {
            if (SelectedTenant == null)
            {
                StatusIsWarning = true;
                StatusMessage = "Select a tenant first.";
                return;
            }

            var dialog = new Views.CreatePersonWindow();
            dialog.Owner = Application.Current.MainWindow;

            if (dialog.ShowDialog() != true) return;

            try
            {
                StatusIsWarning = false;
                StatusMessage = "Creating person...";

                var ok = await _supabaseService.CreatePersonAsync(
                    dialog.PersonName,
                    dialog.PersonRelationshipType,
                    SelectedTenant.TenantId);

                if (ok)
                {
                    StatusMessage = $"Person '{dialog.PersonName}' created.";
                    await LoadPersonsAsync(SelectedTenant.TenantId);
                }
                else
                {
                    StatusIsWarning = true;
                    StatusMessage = "Failed to create person.";
                }
            }
            catch (Exception ex)
            {
                StatusIsWarning = true;
                StatusMessage = $"Error creating person: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] CreatePersonAsync: {ex.Message}");
            }
        }

        private async Task EditPersonAsync()
        {
            if (SelectedTenant == null) return;

            if (SelectedPerson == null)
            {
                StatusIsWarning = true;
                StatusMessage = "Select a person first.";
                return;
            }

            var dialog = new Views.CreatePersonWindow(SelectedPerson);
            dialog.Owner = Application.Current.MainWindow;

            if (dialog.ShowDialog() != true) return;

            try
            {
                StatusIsWarning = false;
                StatusMessage = "Updating person...";

                var ok = await _supabaseService.UpdatePersonAsync(
                    SelectedPerson.PersonId,
                    dialog.PersonName,
                    dialog.PersonRelationshipType);

                if (ok)
                {
                    StatusMessage = $"Person '{dialog.PersonName}' updated.";
                    await LoadPersonsAsync(SelectedTenant.TenantId);
                }
                else
                {
                    StatusIsWarning = true;
                    StatusMessage = "Failed to update person.";
                }
            }
            catch (Exception ex)
            {
                StatusIsWarning = true;
                StatusMessage = $"Error updating person: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] EditPersonAsync: {ex.Message}");
            }
        }

        private async Task DeletePersonAsync()
        {
            if (SelectedTenant == null) return;

            if (SelectedPerson == null)
            {
                StatusIsWarning = true;
                StatusMessage = "Select a person first.";
                return;
            }

            var dialog = new Views.ConfirmDialog($"Delete '{SelectedPerson.DisplayName}'?");
            dialog.Owner = Application.Current.MainWindow;

            if (dialog.ShowDialog() != true) return;

            try
            {
                StatusIsWarning = false;
                StatusMessage = "Deleting person...";

                var ok = await _supabaseService.DeletePersonAsync(SelectedPerson.PersonId);

                if (ok)
                {
                    StatusMessage = "Person deleted.";
                    await LoadPersonsAsync(SelectedTenant.TenantId);
                }
                else
                {
                    StatusIsWarning = true;
                    StatusMessage = "Failed to delete person.";
                }
            }
            catch (Exception ex)
            {
                StatusIsWarning = true;
                StatusMessage = $"Error deleting person: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] DeletePersonAsync: {ex.Message}");
            }
        }

        private async Task CreateEventAsync()
        {
            if (SelectedTenant == null)
            {
                StatusIsWarning = true;
                StatusMessage = "Select a tenant first.";
                return;
            }

            var dialog = new Views.CreateEventWindow();
            dialog.Owner = Application.Current.MainWindow;

            if (dialog.ShowDialog() != true) return;

            try
            {
                StatusIsWarning = false;
                StatusMessage = "Creating event...";

                var ok = await _supabaseService.CreateEventAsync(
                    dialog.EventName,
                    dialog.Description,
                    dialog.StartDate,
                    dialog.EndDate,
                    SelectedTenant.TenantId);

                if (ok)
                {
                    StatusMessage = $"Event '{dialog.EventName}' created.";
                    await LoadEventsAsync(SelectedTenant.TenantId);
                }
                else
                {
                    StatusIsWarning = true;
                    StatusMessage = "Failed to create event.";
                }
            }
            catch (Exception ex)
            {
                StatusIsWarning = true;
                StatusMessage = $"Error creating event: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] CreateEventAsync: {ex.Message}");
            }
        }

        private async Task EditEventAsync()
        {
            if (SelectedTenant == null) return;

            if (SelectedEvent == null)
            {
                StatusIsWarning = true;
                StatusMessage = "Select an event first.";
                return;
            }

            var dialog = new Views.CreateEventWindow(SelectedEvent);
            dialog.Owner = Application.Current.MainWindow;

            if (dialog.ShowDialog() != true) return;

            try
            {
                StatusIsWarning = false;
                StatusMessage = "Updating event...";

                var ok = await _supabaseService.UpdateEventAsync(
                    SelectedEvent.EventId,
                    dialog.EventName,
                    dialog.Description,
                    dialog.StartDate,
                    dialog.EndDate);

                if (ok)
                {
                    StatusMessage = $"Event '{dialog.EventName}' updated.";
                    await LoadEventsAsync(SelectedTenant.TenantId);
                }
                else
                {
                    StatusIsWarning = true;
                    StatusMessage = "Failed to update event.";
                }
            }
            catch (Exception ex)
            {
                StatusIsWarning = true;
                StatusMessage = $"Error updating event: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] EditEventAsync: {ex.Message}");
            }
        }

        private async Task DeleteEventAsync()
        {
            if (SelectedTenant == null) return;

            if (SelectedEvent == null)
            {
                StatusIsWarning = true;
                StatusMessage = "Select an event first.";
                return;
            }

            var dialog = new Views.ConfirmDialog($"Delete '{SelectedEvent.EventName}'?");
            dialog.Owner = Application.Current.MainWindow;

            if (dialog.ShowDialog() != true) return;

            try
            {
                StatusIsWarning = false;
                StatusMessage = "Deleting event...";

                var ok = await _supabaseService.DeleteEventAsync(SelectedEvent.EventId);

                if (ok)
                {
                    StatusMessage = "Event deleted.";
                    await LoadEventsAsync(SelectedTenant.TenantId);
                }
                else
                {
                    StatusIsWarning = true;
                    StatusMessage = "Failed to delete event.";
                }
            }
            catch (Exception ex)
            {
                StatusIsWarning = true;
                StatusMessage = $"Error deleting event: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] DeleteEventAsync: {ex.Message}");
            }
        }

        private async Task CreateTagAsync()
        {
            if (SelectedTenant == null)
            {
                StatusIsWarning = true;
                StatusMessage = "Select a tenant first.";
                return;
            }

            var dialog = new Views.CreateTagWindow(TagCategories);
            dialog.Owner = Application.Current.MainWindow;

            if (dialog.ShowDialog() != true) return;

            try
            {
                StatusIsWarning = false;
                StatusMessage = "Creating tag...";

                var ok = await _supabaseService.CreateTagAsync(
                    dialog.TagName,
                    dialog.TagCategoryId,
                    dialog.Description,
                    dialog.ColorHex,
                    SelectedTenant.TenantId);

                if (ok)
                {
                    StatusMessage = $"Tag '{dialog.TagName}' created.";
                    await LoadTagsAsync(SelectedTenant.TenantId);
                }
                else
                {
                    StatusIsWarning = true;
                    StatusMessage = "Failed to create tag.";
                }
            }
            catch (Exception ex)
            {
                StatusIsWarning = true;
                StatusMessage = $"Error creating tag: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] CreateTagAsync: {ex.Message}");
            }
        }

        public async Task CreateTagCategoryAsync()
        {
            if(SelectedTenant == null)
            {
                StatusIsWarning = true;
                StatusMessage = "Select a tenant first.";
                return;
            }

            var dialog = new Views.CreateTagCategoryWindow();
            dialog.Owner = Application.Current.MainWindow;

            if (dialog.ShowDialog() != true) return;

            try
            {
                StatusIsWarning = false;
                StatusMessage = "Creating tag category...";

                var ok = await _supabaseService.CreateTagCategoryAsync(
                    dialog.CategoryName,
                    "custom_filter",
                    SelectedTenant.TenantId);

                if (ok)
                {
                    StatusMessage = $"Tag category '{dialog.CategoryName}' created.";
                    await LoadTagCategoriesAsync(SelectedTenant.TenantId);
                }
                else
                {
                    StatusIsWarning = true;
                    StatusMessage = "Failed to create tag category.";
                }
            }
            catch (Exception ex)
            {
                StatusIsWarning = true;
                StatusMessage = $"Error creating tag category: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] CreateTagCategoryAsync: {ex.Message}");
            }
        }

        private async Task EditTagAsync()
        {
            if (SelectedTenant == null) return;

            if (SelectedTag == null)
            {
                StatusIsWarning = true;
                StatusMessage = "Select a tag first.";
                return;
            }

            var dialog = new Views.CreateTagWindow(SelectedTag,TagCategories);
            dialog.Owner = Application.Current.MainWindow;

            if (dialog.ShowDialog() != true) return;

            try
            {
                StatusIsWarning = false;
                StatusMessage = "Updating tag...";

                var ok = await _supabaseService.UpdateTagAsync(
                    SelectedTag.TagId,
                    dialog.TagName,
                    dialog.TagCategoryId,
                    dialog.Description,
                    dialog.ColorHex);

                if (ok)
                {
                    StatusMessage = $"Tag '{dialog.TagName}' updated.";
                    await LoadTagsAsync(SelectedTenant.TenantId);
                }
                else
                {
                    StatusIsWarning = true;
                    StatusMessage = "Failed to update tag.";
                }
            }
            catch (Exception ex)
            {
                StatusIsWarning = true;
                StatusMessage = $"Error updating tag: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] EditTagAsync: {ex.Message}");
            }
        }

        private async Task DeleteTagAsync()
        {
            if (SelectedTenant == null) return;

            if (SelectedTag == null)
            {
                StatusIsWarning = true;
                StatusMessage = "Select a tag first.";
                return;
            }

            var dialog = new Views.ConfirmDialog($"Delete '{SelectedTag.TagName}'?");
            dialog.Owner = Application.Current.MainWindow;

            if (dialog.ShowDialog() != true) return;

            try
            {
                StatusIsWarning = false;
                StatusMessage = "Deleting tag...";

                var ok = await _supabaseService.DeleteTagAsync(SelectedTag.TagId);

                if (ok)
                {
                    StatusMessage = "Tag deleted.";
                    await LoadTagsAsync(SelectedTenant.TenantId);
                }
                else
                {
                    StatusIsWarning = true;
                    StatusMessage = "Failed to delete tag.";
                }
            }
            catch (Exception ex)
            {
                StatusIsWarning = true;
                StatusMessage = $"Error deleting tag: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] DeleteTagAsync: {ex.Message}");
            }
        }

        private async Task LoadTagCategoriesAsync(string tenantId)
        {
            try
            {
                var categories = await _supabaseService.GetTagCategoriesAsync(tenantId);
                TagCategories.Clear();
                foreach (var c in categories) TagCategories.Add(c);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] LoadTagCategoriesAsync: {ex.Message}");
            }
        }

        private async Task LoadMapAsync()
        {
            if (SelectedTenant == null)
            {
                StatusIsWarning = true;
                StatusMessage = "Select a tenant first.";
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedFolder))
            {
                StatusIsWarning = true;
                StatusMessage = "Select a folder first using Browse Folder.";
                return;
            }

            if (MediaFiles.Any())
            {
                StatusIsWarning = true;
                StatusMessage = "The current folder contains gallery images. " +
                                "Select a folder with map images only using Browse Folder, then click Load Map.";
                return;
            }

            var supportedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            var files = Directory.GetFiles(SelectedFolder)
                .Where(f => supportedExtensions.Contains(
                    Path.GetExtension(f).ToLowerInvariant()))
                .ToList();

            if (files.Count == 0)
            {
                StatusIsWarning = true;
                StatusMessage = "No image files found in selected folder.";
                return;
            }

            try
            {
                AppPathsService.EnsureFoldersExist();

                int loaded = 0;
                int failed = 0;
                int skipped = 0;

                StatusIsWarning = false;
                StatusMessage = $"Loading {files.Count} map image(s)...";

                foreach (var file in files)
                {
                    try
                    {
                        var destination = AppPathsService.GetMapDestinationPath(file);
                        if (!File.Exists(destination))
                            new FileInfo(file).CopyTo(destination);

                        var ext = Path.GetExtension(file).ToLowerInvariant();
                        var mimeType = ext switch
                        {
                            ".jpg" or ".jpeg" => "image/jpeg",
                            ".png" => "image/png",
                            _ => "application/octet-stream"
                        };

                        var exists = await _supabaseService.VisualAssetExistsAsync(
                            destination, SelectedTenant.TenantId);

                        if (exists)
                        {
                            skipped++;
                            continue;
                        }

                        var ok = await _supabaseService.CreateVisualAssetAsync(
                            destination, mimeType, SelectedTenant.TenantId);

                        if (ok) loaded++;
                        else failed++;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        System.Diagnostics.Debug.WriteLine($"[MainViewModel] LoadMapAsync - {file}: {ex.Message}");
                    }
                }

                StatusIsWarning = failed > 0;
                var parts = new List<string>();
                if (loaded > 0) parts.Add($"{loaded} loaded");
                if (failed > 0) parts.Add($"{failed} failed");
                if (skipped > 0) parts.Add($"{skipped} already existed");
                StatusMessage = $"Maps: {string.Join(", ", parts)}.";
            }
            catch (Exception ex)
            {
                StatusIsWarning = true;
                StatusMessage = $"Error loading maps: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] LoadMapAsync: {ex.Message}");
            }
        }

        private async Task AssignMapAsync()
        {
            if (SelectedTenant == null)
            {
                StatusIsWarning = true;
                StatusMessage = "Select a tenant first.";
                return;
            }

            if (SelectedLocation == null)
            {
                StatusIsWarning = true;
                StatusMessage = "Select a location first.";
                return;
            }

            try
            {
                StatusIsWarning = false;
                StatusMessage = "Loading available maps...";

                var maps = await _supabaseService.GetMapVisualAssetsAsync(SelectedTenant.TenantId);

                if (maps.Count == 0)
                {
                    StatusIsWarning = true;
                    StatusMessage = "No maps available. Load map images first.";
                    return;
                }

                var dialog = new Views.SelectMapDialog(maps);
                dialog.Owner = Application.Current.MainWindow;

                if (dialog.ShowDialog() != true) return;

                StatusMessage = "Assigning map...";

                var ok = await _supabaseService.AssignMapToLocationAsync(
                    SelectedLocation.LocationId,
                    dialog.SelectedMap!.VisualAssetId);

                StatusIsWarning = !ok;
                StatusMessage = ok
                    ? $"Map assigned to '{SelectedLocation.LocationName}'."
                    : "Failed to assign map.";
            }
            catch (Exception ex)
            {
                StatusIsWarning = true;
                StatusMessage = $"Error assigning map: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] AssignMapAsync: {ex.Message}");
            }
        }

        private async Task RenderImage()
        {
            if (SelectedMedia?.MediaAssetId == null || SelectedTenant == null) return;

            StatusMessage = "Loadion metadata...";
            StatusIsWarning = false;

            var renderData = await _supabaseService.GetMediaRenderDataAsync(
                SelectedMedia.MediaAssetId,
                SelectedTenant.TenantId,
                SelectedMedia.FullPath,
                SelectedMedia.CaptureTimestamp);

            if (renderData != null)
            {
                StatusIsWarning = true ;
                StatusMessage = "Could not load metadata for render.";
                return;
            }

            StatusMessage = $"Metadata loades for: {SelectedMedia.FileName}";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}