using System.ComponentModel;

namespace BIN;

public class BOQGasSizeItem : INotifyPropertyChanged
{
	private string _name;

	private bool _isChecked = true;

	public string Name
	{
		get
		{
			return _name;
		}
		set
		{
			_name = value;
			OnPropertyChanged("Name");
		}
	}

	public bool IsChecked
	{
		get
		{
			return _isChecked;
		}
		set
		{
			_isChecked = value;
			OnPropertyChanged("IsChecked");
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	protected void OnPropertyChanged(string propertyName)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
