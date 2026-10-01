using System;
using System.Windows.Input;

namespace BIN.Common.Mvvm;

public class SBP_RelayCommand : ICommand
{
	private readonly Action<object> _execute;

	private readonly Predicate<object> _canExecute;

	public event EventHandler CanExecuteChanged
	{
		add
		{
			CommandManager.RequerySuggested += value;
		}
		remove
		{
			CommandManager.RequerySuggested -= value;
		}
	}

	public SBP_RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
	{
		_execute = execute ?? throw new ArgumentNullException("execute");
		_canExecute = canExecute;
	}

	public bool CanExecute(object parameter)
	{
		return _canExecute == null || _canExecute(parameter);
	}

	public void Execute(object parameter)
	{
		_execute(parameter);
	}
}
