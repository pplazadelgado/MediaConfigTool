using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MediaConfigTool.ViewModels
{
    public class RelayCommand : ICommand
    {
        private readonly Action? _execute;
        private readonly Func<Task>? _executeAsync;

        public RelayCommand(Action execute)
        {
            _execute = execute;
        }

        public RelayCommand(Func<Task> executeAsync)
        {
            _executeAsync = executeAsync;
        }

        public bool CanExecute(object? parameter) => true;

        public async void Execute(object? parameter)
        {
            if (_executeAsync is not null)
                await _executeAsync();
            else
                _execute?.Invoke();
        }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }
    }
}