using System.ComponentModel;
using Autodesk.Revit.DB;

namespace BIN;

public class ViewItem : INotifyPropertyChanged
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

	public string ViewType { get; set; }

	public View ViewRef { get; set; }

	public event PropertyChangedEventHandler PropertyChanged;

	public ViewItem(View v)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		ViewRef = v;
		Name = ((Element)v).Name;
		ViewType viewType = v.ViewType;
		ViewType = viewType.ToString();
		_isSelected = false;
	}

	protected void OnPropertyChanged(string name)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
