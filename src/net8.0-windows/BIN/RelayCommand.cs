using System;
using System.Windows.Input;

namespace BIN;

public class RelayCommand : ICommand
{
	private readonly Action<object> _e;

	public event EventHandler CanExecuteChanged;

	public RelayCommand(Action<object> e)
	{
		_e = e;
	}

	public bool CanExecute(object p)
	{
		return true;
	}

	public void Execute(object p)
	{
		_e(p);
	}
}
