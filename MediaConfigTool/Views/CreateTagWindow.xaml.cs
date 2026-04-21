using System.Collections.Generic;
using System.Windows;
using MediaConfigTool.Models;

namespace MediaConfigTool.Views
{
    public partial class CreateTagWindow : Window
    {
        public string TagName { get; private set; } = string.Empty;
        public string TagCategoryId { get; private set; } = string.Empty;
        public string? Description { get; private set; }
        public string? ColorHex { get; private set; }

        private readonly bool _isEditMode;

        public CreateTagWindow(IEnumerable<TagCategory> categories)
        {
            InitializeComponent();
            CategoryBox.ItemsSource = new List<TagCategory>(categories);
        }

        public CreateTagWindow(Tag tag, IEnumerable<TagCategory> categories) : this(categories)
        {
            _isEditMode = true;
            Title = "Edit Tag";
            NameBox.Text = tag.TagName;
            DescriptionBox.Text = tag.Description;
            ColorHexBox.Text = tag.ColorHex;

            foreach (TagCategory item in CategoryBox.Items)
            {
                if (item.TagCategoryId == tag.TagCategoryId)
                {
                    CategoryBox.SelectedItem = item;
                    break;
                }
            }

            var header = FindName("HeaderText") as System.Windows.Controls.TextBlock;
            if (header != null) header.Text = "Edit Tag";
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                NameBox.BorderBrush = System.Windows.Media.Brushes.Red;
                return;
            }

            if (CategoryBox.SelectedItem is not TagCategory selectedCategory)
            {
                CategoryBorder.BorderBrush = System.Windows.Media.Brushes.Red;
                return;
            }

            TagName = NameBox.Text.Trim();
            TagCategoryId = selectedCategory.TagCategoryId;
            Description = string.IsNullOrWhiteSpace(DescriptionBox.Text)
                ? null : DescriptionBox.Text.Trim();
            ColorHex = string.IsNullOrWhiteSpace(ColorHexBox.Text)
                ? null : ColorHexBox.Text.Trim();

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