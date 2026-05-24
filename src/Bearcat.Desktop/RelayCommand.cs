using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Bearcat.Desktop;

public sealed class RelayCommand(Action<object?> execute) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter)
    {
        return true;
    }

    public void Execute(object? parameter)
    {
        execute(parameter);
    }
}

public sealed class AsyncCommand(Func<object?, Task> execute) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter)
    {
        return true;
    }

    public async void Execute(object? parameter)
    {
        await execute(parameter);
    }
}
