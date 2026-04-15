using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MediaConfigTool.ViewModels
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object?>? _execute;
        private readonly Func<Task>? _executeAsync;
        private readonly Func<object?, bool>? _canExecute;

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public RelayCommand(Func<Task> executeAsync, Func<object?, bool>? canExecute = null)
        {
            _executeAsync = executeAsync;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
            => _canExecute == null || _canExecute(parameter);

        public async void Execute(object? parameter)
        {
            if (_executeAsync is not null)
                await _executeAsync();
            else
                _execute?.Invoke(parameter);
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}