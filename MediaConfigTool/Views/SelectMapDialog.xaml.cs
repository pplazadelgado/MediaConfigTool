using System.Collections.Generic;
using System.Windows;
using MediaConfigTool.Models;

namespace MediaConfigTool.Views
{
    /// <summary>
    /// Lógica de interacción para SelectMapDialog.xaml
    /// </summary>
    public partial class SelectMapDialog : Window
    {
        public VisualAsset? SelectedMap {  get; private set; }
        public SelectMapDialog(IEnumerable<VisualAsset> maps)
        {
            InitializeComponent();
            MapListBox.ItemsSource = maps;
        }

        private void Assing_Click(object sender, RoutedEventArgs e)
        {
            if(MapListBox.SelectedItem is not VisualAsset selected)
            {
                MessageBox.Show("Select a map first.", "Narrim",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SelectedMap =selected;
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
