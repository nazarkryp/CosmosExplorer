using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CosmosDbManager.Infrastructure;

public sealed class AsyncCommand(Func<Task> execute, Func<bool> canExecute) : ICommand, INotifyPropertyChanged
{
    private readonly Func<Task> _execute = execute;
    private readonly Func<bool> _canExecute = canExecute;

    private bool _isExecuting;

    public event EventHandler? CanExecuteChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsExecuting
    {
        get => _isExecuting;
        set
        {
            if (value == _isExecuting)
            {
                return;
            }

            _isExecuting = value;

            OnPropertyChanged();
        }
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool CanExecute(object parameter)
    {
        return !_isExecuting && _canExecute();
    }

    public async void Execute(object parameter)
    {
        if (CanExecute(parameter))
        {
            try
            {
                IsExecuting = true;

                RaiseCanExecuteChanged();

                await _execute();
            }
            finally
            {
                IsExecuting = false;

                RaiseCanExecuteChanged();
            }
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}