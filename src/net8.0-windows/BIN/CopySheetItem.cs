using System.ComponentModel;
using Autodesk.Revit.DB;

namespace BIN;

public class CopySheetItem : INotifyPropertyChanged
{
	private bool _isSelected;

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

	public string Name { get; set; }

	public string Number { get; set; }

	public ViewSheet SheetRef { get; set; }

	public event PropertyChangedEventHandler PropertyChanged;

	public CopySheetItem(ViewSheet s)
	{
		SheetRef = s;
		Name = ((Element)s).Name;
		Number = s.SheetNumber;
		_isSelected = false;
	}

	protected void OnPropertyChanged(string name)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
