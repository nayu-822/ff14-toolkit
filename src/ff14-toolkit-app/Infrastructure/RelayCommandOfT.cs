using System.Windows.Input;

namespace FF14Toolkit.App.Infrastructure;

public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T?> execute;
    private readonly Predicate<T?>? canExecute;

    public RelayCommand(Action<T?> execute, Predicate<T?>? canExecute = null)
    {
        this.execute = execute;
        this.canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return canExecute?.Invoke(ConvertParameter(parameter)) ?? true;
    }

    public void Execute(object? parameter)
    {
        execute(ConvertParameter(parameter));
    }

    public void NotifyCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    private static T? ConvertParameter(object? parameter)
    {
        if (parameter is null)
        {
            return default;
        }

        if (parameter is T typedParameter)
        {
            return typedParameter;
        }

        return (T?)parameter;
    }
}
