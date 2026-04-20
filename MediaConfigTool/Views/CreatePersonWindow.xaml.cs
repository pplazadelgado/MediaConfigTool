using System.Windows;
using System.Windows.Controls;
using MediaConfigTool.Models;

namespace MediaConfigTool.Views
{
    public partial class CreatePersonWindow : Window
    {
        public string PersonName { get; private set; } = string.Empty;
        public string? PersonRelationshipType { get; private set; }

        private readonly bool _isEditMode;

        // Edit mode constructor
        public CreatePersonWindow(Person person) : this()
        {
            _isEditMode = true;
            Title = "Edit Person";
            NameBox.Text = person.DisplayName;

            // Select matching type in combo
            foreach (ComboBoxItem item in TypeBox.Items)
            {
                if (item.Content?.ToString() == person.RelationshipType)
                {
                    TypeBox.SelectedItem = item;
                    break;
                }
            }

            var header = FindName("HeaderText") as System.Windows.Controls.TextBlock;
            if (header != null) header.Text = "Edit Person";
        }

        public CreatePersonWindow()
        {
            InitializeComponent();
            TypeBox.SelectedIndex = 0; // "None"
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                NameBox.BorderBrush = System.Windows.Media.Brushes.Red;
                return;
            }

            PersonName = NameBox.Text.Trim();
            var selected = (TypeBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
            PersonRelationshipType = selected == "None" ? null : selected;

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
