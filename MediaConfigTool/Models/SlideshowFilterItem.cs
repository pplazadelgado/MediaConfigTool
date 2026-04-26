using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MediaConfigTool.Models
{
    public class YearFilterItem : INotifyPropertyChanged
    {
        public int Year {  get; set; }
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class EventFilterItem : INotifyPropertyChanged
    {
        public string EventId { get; set; } = string.Empty;
        public string EventName { get; set; } = string.Empty;
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(name));
    }

    public class PersonFilterItem : INotifyPropertyChanged
    {
        public string PersonId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class TagFilterItem : INotifyPropertyChanged
    {
        public string TagId { get; set; } = string.Empty;
        public string TagName { get; set; } = string.Empty;
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
