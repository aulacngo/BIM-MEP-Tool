using System.ComponentModel;
using Autodesk.Revit.DB;

namespace BIN;

public class SupportTypeItem : INotifyPropertyChanged
{
	private bool _isSelected = true;

	public string TypeName { get; set; }

	public ElementId SymbolId { get; set; }

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
				this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsSelected"));
			}
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;
}
