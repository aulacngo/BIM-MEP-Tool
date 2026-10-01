using System.ComponentModel;
using Autodesk.Revit.DB;

namespace BIN;

public class RenameItem : INotifyPropertyChanged
{
	private bool _isSelected;

	private string _displayName;

	public string DisplayName
	{
		get
		{
			return _displayName;
		}
		set
		{
			_displayName = value;
			OnPropertyChanged("DisplayName");
		}
	}

	public ElementId ElementId { get; set; }

	public bool IsSelected
	{
		get
		{
			return _isSelected;
		}
		set
		{
			_isSelected = value;
			OnPropertyChanged("IsSelected");
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	private void OnPropertyChanged(string p)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
	}
}
