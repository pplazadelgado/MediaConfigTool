using MediaConfigTool.Models;
using MediaConfigTool.Services;
using MediaConfigTool.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MediaConfigTool
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            AppPathsService.EnsureFoldersExist();
            DataContext = new MainViewModel();

            if (DataContext is MainViewModel vm)
            {
                vm.HighlightedLocationIds.CollectionChanged += (_, _) => ApplyLocationHighlights(vm);
                vm.HighlightedPersonIds.CollectionChanged += (_, _) => ApplyPersonHighlights(vm);
                vm.HighlightedEventIds.CollectionChanged += (_, _) => ApplyEventHighlights(vm);
                vm.HighlightedTagIds.CollectionChanged += (_, _) => ApplyTagHighlights(vm);
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                await vm.LoadTenantsAsync();
        }

        private void MediaGallery_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is MainViewModel vm && sender is ListView lv)
                vm.OnSelectionChanged(lv.SelectedItems);
        }

        private void LocationListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(DataContext is MainViewModel vm && sender is ListBox lb)
                vm.OnLocationSelectionChanged(lb.SelectedItems);
        }

        private void PersonListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(DataContext is MainViewModel vm && sender is ListBox lb)
                vm.OnPersonSelectionChanged(lb.SelectedItems);
        }

        private void EventListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is MainViewModel vm && sender is ListBox lb)
                vm.OnEventSelectionChanged(lb.SelectedItems);
        }

        private void TagListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(DataContext is MainViewModel vm && sender is ListBox lb)
                vm.OnTagSelectionChanged(lb.SelectedItems);
        }
            

        private void FolderTree_Expanded(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is TreeViewItem item &&
                item.DataContext is FolderItem folder)
            {
                folder.LoadSubFolders();
            }
        }

        private async void FolderTree_Selected(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            if (e.OriginalSource is TreeViewItem item &&
                item.DataContext is FolderItem folder &&
                DataContext is MainViewModel vm)
            {
                await vm.SelectFolderAsync(folder.FullPath);
            }
        }

        private void FolderTree_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!e.Handled && sender is DependencyObject dep)
            {
                var sv = FindParent<ScrollViewer>(dep);
                if (sv != null)
                {
                    e.Handled = true;
                    sv.ScrollToVerticalOffset(sv.VerticalOffset - (e.Delta / 3.0));
                }
            }
        }

        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is T match) return match;
                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            DragMove();
        }

        private void ApplyLocationHighlights(MainViewModel vm)
        {
            LocationListBox.SelectionChanged -= LocationListBox_SelectionChanged;
            LocationListBox.SelectedItems.Clear();
            foreach (var item in LocationListBox.Items)
                if (item is Location loc && vm.HighlightedLocationIds.Contains(loc.LocationId))
                    LocationListBox.SelectedItems.Add(item);
            LocationListBox.SelectionChanged += LocationListBox_SelectionChanged;
        }

        private void ApplyPersonHighlights(MainViewModel vm)
        {
            PersonListBox.SelectionChanged -= PersonListBox_SelectionChanged;
            PersonListBox.SelectedItems.Clear();
            foreach (var item in PersonListBox.Items)
                if (item is Person p && vm.HighlightedPersonIds.Contains(p.PersonId))
                    PersonListBox.SelectedItems.Add(item);
            PersonListBox.SelectionChanged += PersonListBox_SelectionChanged;
        }

        private void ApplyEventHighlights(MainViewModel vm)
        {
            EventListBox.SelectionChanged -= EventListBox_SelectionChanged;
            EventListBox.SelectedItems.Clear();
            foreach (var item in EventListBox.Items)
                if (item is Event e && vm.HighlightedEventIds.Contains(e.EventId))
                    EventListBox.SelectedItems.Add(item);
            EventListBox.SelectionChanged += EventListBox_SelectionChanged;
        }

        private void ApplyTagHighlights(MainViewModel vm)
        {
            TagListBox.SelectionChanged -= TagListBox_SelectionChanged;
            TagListBox.SelectedItems.Clear();
            foreach (var item in TagListBox.Items)
                if (item is Tag t && vm.HighlightedTagIds.Contains(t.TagId))
                    TagListBox.SelectedItems.Add(item);
            TagListBox.SelectionChanged += TagListBox_SelectionChanged;
        }
    }
}