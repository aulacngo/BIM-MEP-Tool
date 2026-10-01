using System.ComponentModel;
using Autodesk.Revit.DB;

namespace BIN;

public class TransferDocumentItem : INotifyPropertyChanged
{
	private bool _isSelected;

	public string Name { get; set; }

	public ElementId ElementId { get; set; }

	public bool IsSelected
	{
		get
		{
			return _isSelected;
		}
		set
		{
			if (_isSelected != value)
			{
				_isSelected = value;
				OnPropertyChanged("IsSelected");
			}
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	protected void OnPropertyChanged(string propertyName)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
