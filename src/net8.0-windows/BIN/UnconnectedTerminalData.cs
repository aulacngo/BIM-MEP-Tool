using System.ComponentModel;

namespace BIN;

public class UnconnectedTerminalData : INotifyPropertyChanged
{
	private bool _isResolved;

	public int Index { get; set; }

	public string Id { get; set; }

	public bool IsResolved
	{
		get
		{
			return _isResolved;
		}
		set
		{
			if (_isResolved != value)
			{
				_isResolved = value;
				this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsResolved"));
			}
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;
}
