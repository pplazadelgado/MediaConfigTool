using System;
using System.Windows;
using MediaConfigTool.Models;

namespace MediaConfigTool.Views
{
    public partial class CreateEventWindow : Window
    {
        public string EventName { get; private set; } = string.Empty;
        public string? Description { get; private set; }
        public DateTimeOffset? StartDate { get; private set; }
        public DateTimeOffset? EndDate { get; private set; }

        private readonly bool _isEditMode;

        public CreateEventWindow(Event evt) : this()
        {
            _isEditMode = true;
            Title = "Edit Event";
            NameBox.Text = evt.EventName;
            DescriptionBox.Text = evt.Description;

            if (evt.StartTimestamp.HasValue)
                StartDatePicker.SelectedDate = evt.StartTimestamp.Value.LocalDateTime;

            if (evt.EndTimestamp.HasValue)
                EndDatePicker.SelectedDate = evt.EndTimestamp.Value.LocalDateTime;

            var header = FindName("HeaderText") as System.Windows.Controls.TextBlock;
            if (header != null) header.Text = "Edit Event";
        }

        public CreateEventWindow()
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

            EventName = NameBox.Text.Trim();
            Description = string.IsNullOrWhiteSpace(DescriptionBox.Text) ? null : DescriptionBox.Text.Trim();
            StartDate = StartDatePicker.SelectedDate.HasValue
                ? new DateTimeOffset(StartDatePicker.SelectedDate.Value, TimeSpan.Zero)
                : null;
            EndDate = EndDatePicker.SelectedDate.HasValue
                ? new DateTimeOffset(EndDatePicker.SelectedDate.Value, TimeSpan.Zero)
                : null;

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
