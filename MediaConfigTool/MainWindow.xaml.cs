using MediaConfigTool.ViewModels;
using System.Windows;


namespace MediaConfigTool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                await vm.LoadTenantAsync();
        }
    }
}