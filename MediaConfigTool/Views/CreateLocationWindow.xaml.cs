using System.Windows;
using System.Windows.Controls;
using MediaConfigTool.Models;

namespace MediaConfigTool.Views
{
    public partial class CreateLocationWindow : Window
    {
        public string LocationName { get; private set; } = string.Empty;
        public string LocationType { get; private set; } = string.Empty;

        private readonly bool _isEditMode;

        // Edit mode constructor
        public CreateLocationWindow(Location location) : this()
        {
            _isEditMode = true;
            Title = "Edit Location";
            NameBox.Text = location.LocationName;

            // Select matching type in combo
            foreach (ComboBoxItem item in TypeBox.Items)
            {
                if (item.Content?.ToString() == location.LocationType)
                {
                    TypeBox.SelectedItem = item;
                    break;
                }
            }

            // Update header text
            var header = FindName("HeaderText") as System.Windows.Controls.TextBlock;
            if (header != null) header.Text = "Edit Location";
        }

        public CreateLocationWindow()
        {
            InitializeComponent();
            TypeBox.SelectedIndex = 0;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                NameBox.BorderBrush = System.Windows.Media.Brushes.Red;
                return;
            }

            LocationName = NameBox.Text.Trim();
            LocationType = (TypeBox.SelectedItem as ComboBoxItem)?.Content?.ToString()
                           ?? "custom_place";

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