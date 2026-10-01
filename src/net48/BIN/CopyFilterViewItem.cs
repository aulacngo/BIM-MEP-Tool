using System.ComponentModel;
using Autodesk.Revit.DB;

namespace BIN;

public class CopyFilterViewItem : INotifyPropertyChanged
{
	private bool _isChecked;

	public string Name { get; set; }

	public ElementId Id { get; set; }

	public string ViewTypeStr { get; set; }

	public bool IsChecked
	{
		get
		{
			return _isChecked;
		}
		set
		{
			if (_isChecked != value)
			{
				_isChecked = value;
				this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsChecked"));
			}
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;
}
