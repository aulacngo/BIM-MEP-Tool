using System.ComponentModel;
using Autodesk.Revit.DB;

namespace BIN;

public class SelectableElement : INotifyPropertyChanged
{
	private bool _isSelected;

	public ElementId Id { get; set; }

	public string Name { get; set; }

	public string DisplayName { get; set; }

	public string SheetNumber { get; set; }

	public string ViewTypeName { get; set; }

	public bool IsSelected
	{
		get
		{
			return _isSelected;
		}
		set
		{
			_isSelected = value;
			this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsSelected"));
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;
}
