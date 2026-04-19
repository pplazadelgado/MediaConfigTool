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
            new AppPathsService().EnsureFolderExist();
            DataContext = new MainViewModel();
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
    }
}