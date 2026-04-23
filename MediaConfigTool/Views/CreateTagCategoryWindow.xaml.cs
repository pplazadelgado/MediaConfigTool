using System.Windows;

namespace MediaConfigTool.Views
{
    /// <summary>
    /// Lógica de interacción para CreateTagCategoryWindow.xaml
    /// </summary>
    public partial class CreateTagCategoryWindow : Window
    {
        public string CategoryName { get; private set; } = string.Empty;
        public CreateTagCategoryWindow()
        {
            InitializeComponent();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                NameBox.BorderBrush = System.Windows.Media.Brushes.Red;
                return;
            }

            CategoryName = NameBox.Text.Trim();
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
