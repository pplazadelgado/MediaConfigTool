using System.Windows;
using MediaConfigTool.Models;
using MediaConfigTool.Services;
using System.IO;
using System.Collections.ObjectModel;



namespace MediaConfigTool.Views
{
    /// <summary>
    /// Lógica de interacción para SlideshowWindow.xaml
    /// </summary>
    public partial class SlideshowWindow : Window
    {
        public readonly SupabaseService _supabaseService;
        private readonly string _tenantId;
        private readonly SlideshowService _slideshowService;

        public ObservableCollection<YearFilterItem> Years { get; } = new();
        public ObservableCollection<EventFilterItem> Events { get; } = new();
        public ObservableCollection<PersonFilterItem> Persons { get; } = new();
        public ObservableCollection<TagFilterItem> Tags { get; } = new();

        public SlideshowWindow(SupabaseService supabaseService, string tenantId)
        {
            InitializeComponent();
            _supabaseService = supabaseService;
            _tenantId = tenantId;
            _slideshowService = new SlideshowService(_supabaseService);

            YearList.ItemsSource = Years;
            EventList.ItemsSource = Events;
            PersonList.ItemsSource = Persons;
            TagList.ItemsSource= Tags;

            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender,RoutedEventArgs e)
        {
            StatusText.Text = "Loading filters...";

            //Years
            var years = await _supabaseService.GetImportedYearAsync(_tenantId);
            foreach (var y in years)
                Years.Add(new YearFilterItem { Year = y });

            // Events
            var events = await _supabaseService.GetEventsAsync(_tenantId);
            foreach (var ev in events)
                Events.Add(new EventFilterItem
                {
                    EventId = ev.EventId,
                    EventName = ev.EventName
                });

            // Persons
            var persons = await _supabaseService.GetPersonAsync(_tenantId);
            foreach (var p in persons)
                Persons.Add(new PersonFilterItem
                {
                    PersonId = p.PersonId,
                    DisplayName = p.DisplayName
                });

            // Tags
            var tags = await _supabaseService.GetTagsAsync(_tenantId);
            foreach (var t in tags)
                Tags.Add(new TagFilterItem
                {
                    TagId = t.TagId,
                    TagName = t.TagName
                });

            StatusText.Text = "Ready.";
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BrowseDirectory_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select target folder",
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Select Folder",
                Filter = "Folders|*.",
                ValidateNames = false
            };

            if (dialog.ShowDialog() == true)
                DirectoryBox.Text = System.IO.Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
        }

        private async void CreateSlideshow_Click(object sender, RoutedEventArgs e)
        {
            var name = SlideshowNameBox.Text.Trim();
            var directory = DirectoryBox.Text.Trim();

            if (string.IsNullOrEmpty(name))
            {
                StatusText.Text = "Please enter a slideshow name.";
                return;
            }

            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                StatusText.Text = "Please select a valid target directory.";
                return;
            }

            // Collect filters
            var selectedYears = Years.Where(y => y.IsSelected).Select(y => y.Year).ToList();
            var selectedEventIds = Events.Where(ev => ev.IsSelected).Select(ev => ev.EventId).ToList();
            var selectedPersonIds = Persons.Where(p => p.IsSelected).Select(p => p.PersonId).ToList();
            var selectedTagIds = Tags.Where(t => t.IsSelected).Select(t => t.TagId).ToList();

            // Orientation
            bool isPortrait = OrientationPortrait.IsChecked == true;
            bool isAuto = OrientationAuto.IsChecked == true;

            // Resolution
            int resolution = 1200;

            StatusText.Text = "Filtering images...";
            CreateButton.IsEnabled = false;

            var assetIds = await _slideshowService.GetFilteredAssetIdsAsync(
                _tenantId, selectedYears, selectedEventIds, selectedPersonIds, selectedTagIds);

            if (assetIds.Count == 0)
            {
                StatusText.Text = "No images found with selected filters.";
                CreateButton.IsEnabled = true;
                return;
            }

            // Create output folder
            var outputFolder = Path.Combine(directory, name);
            Directory.CreateDirectory(outputFolder);

            int counter = 1;
            int total = assetIds.Count;

            foreach (var (assetId,captureTs) in assetIds)
            {
                StatusText.Text = $"Rendering {counter} of {total}...";

                // Get file path
                var fileUri = await _slideshowService.GetFileUriForAssetAsync(assetId, _tenantId);
                if (string.IsNullOrEmpty(fileUri) || !File.Exists(fileUri))
                {
                    counter++;
                    continue;
                }

                // Get capture timestamp
                
                var renderData = await _supabaseService.GetMediaRenderDataAsync(
                    assetId, _tenantId, fileUri, captureTs);

                if (renderData == null)
                {
                    counter++;
                    continue;
                }

                // Auto orientation — based on image dimensions
                bool portrait = isPortrait;
                if (isAuto)
                {
                    try
                    {
                        var bmp = new System.Windows.Media.Imaging.BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource = new Uri(fileUri);
                        bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        portrait = bmp.PixelHeight > bmp.PixelWidth;
                    }
                    catch { portrait = false; }
                }

                var bitmap = await _slideshowService.RenderOffscreenAsync(renderData, portrait);

                var fileName = $"{name}_{counter:D3}.png";
                var filePath = Path.Combine(outputFolder, fileName);

                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                using var stream = File.OpenWrite(filePath);
                encoder.Save(stream);

                counter++;
            }

            StatusText.Text = $"Done. {total} images saved to {outputFolder}";
            CreateButton.IsEnabled = true;
        }
    }
}
