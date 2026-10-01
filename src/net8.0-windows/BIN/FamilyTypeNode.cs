using System.ComponentModel;
using Autodesk.Revit.DB;

namespace BIN;

public class FamilyTypeNode : INotifyPropertyChanged
{
	private bool _isChecked = false;

	private string _name;

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

	public FamilySymbol Symbol { get; set; }

	public FamilyTreeNode Parent { get; set; }

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
				OnPropertyChanged("IsChecked");
				Parent?.UpdateCheckState();
			}
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public void SetIsChecked(bool value)
	{
		if (_isChecked != value)
		{
			_isChecked = value;
			OnPropertyChanged("IsChecked");
		}
	}

	protected void OnPropertyChanged(string p)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
	}
}
