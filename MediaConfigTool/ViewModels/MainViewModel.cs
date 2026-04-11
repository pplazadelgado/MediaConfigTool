using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Win32;
using MediaConfigTool.Models;
using MediaConfigTool.Services;
namespace MediaConfigTool.ViewModels
{
    internal class MainViewModel : INotifyPropertyChanged
    {
        private readonly SupabaseService _supabaseService;
        private readonly MediaFileService _mediaFileService;

        public ObservableCollection<Tenant> Tenants { get; } = new();
        public ObservableCollection<MediaFile> MediaFiles { get; } = new();



        private Tenant? _selectedTenant;
        public Tenant? SelectedTenant
        {
            get => _selectedTenant;
            set { _selectedTenant = value;  OnPropertyChanged(); }
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

        public ICommand BrowseFolderCommand { get; }

        public MainViewModel()
        {
            _supabaseService = new SupabaseService();
            _mediaFileService = new MediaFileService();

            BrowseFolderCommand = new RelayCommand(BrowseFolder);
        }

        public async Task LoadTenantAsync()
        {
            try
            {
                StatusMessage = "Loading tenants....";
                var tenants = await _supabaseService.GetTenantAsync();
                Tenants.Clear();
                foreach (var t in tenants)
                    Tenants.Add(t);
                StatusMessage = tenants.Count > 0 ? "Tenants loaded." : "No tenants found.";
            }
            catch(Exception ex)
            {
                StatusMessage = "Failed to load tenants.";
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] {ex.Message}");
            }

        }

        private void BrowseFolder()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select media folder"
            };

            var owner = System.Windows.Application.Current.MainWindow;

            if(dialog.ShowDialog(owner) == true)
            {
                SelectedFolder = dialog.FolderName;
                LoadMediaFiles(SelectedFolder);
            }
        }

        private async void LoadMediaFiles(string folderPath)
        {
            try
            {
                MediaFiles.Clear();
                StatusMessage = "Scanning folder...";

                var files = _mediaFileService.GetMediaFiles(folderPath);

                if (files.Count == 0)
                {
                    StatusMessage = "No images found in selected folder.";
                    return;
                }

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
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] LoadMediaFiles: {ex.Message}");
            }
        }


        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
