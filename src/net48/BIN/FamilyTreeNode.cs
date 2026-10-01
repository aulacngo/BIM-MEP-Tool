using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Autodesk.Revit.DB;

namespace BIN;

public class FamilyTreeNode : INotifyPropertyChanged
{
	private bool? _isChecked = false;

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

	public Family Family { get; set; }

	public ObservableCollection<FamilyTypeNode> Children { get; set; }

	public bool? IsChecked
	{
		get
		{
			return _isChecked;
		}
		set
		{
			if (_isChecked == value)
			{
				return;
			}
			_isChecked = value;
			OnPropertyChanged("IsChecked");
			if (!value.HasValue || Children == null)
			{
				return;
			}
			foreach (FamilyTypeNode child in Children)
			{
				child.SetIsChecked(value.Value);
			}
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public void UpdateCheckState()
	{
		if (Children != null && Children.Count != 0)
		{
			bool all = Children.All((FamilyTypeNode c) => c.IsChecked);
			bool none = Children.All((FamilyTypeNode c) => !c.IsChecked);
			bool? newValue = (all ? new bool?(true) : (none ? new bool?(false) : ((bool?)null)));
			if (_isChecked != newValue)
			{
				_isChecked = newValue;
				OnPropertyChanged("IsChecked");
			}
		}
	}

	protected void OnPropertyChanged(string p)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
	}
}
