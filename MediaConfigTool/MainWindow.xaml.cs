using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MediaConfigTool.Models;
using MediaConfigTool.ViewModels;

namespace MediaConfigTool
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                await vm.LoadTenantsAsync();
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
            if (e.OriginalSource is TreeViewItem item &&
                item.DataContext is FolderItem folder &&
                DataContext is MainViewModel vm)
            {
                await vm.SelectFolderAsync(folder.FullPath);
            }
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