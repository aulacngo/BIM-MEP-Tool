using System.ComponentModel;
using System.Runtime.CompilerServices;
using Autodesk.Revit.DB;

namespace BIN;

public class MEPCategoryItem : INotifyPropertyChanged
{
	private bool _isChecked = true;

	public string Name { get; set; }

	public BuiltInCategory CategoryId { get; set; }

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

	public MEPCategoryItem(string n, BuiltInCategory id)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		Name = n;
		CategoryId = id;
	}

	private void OnPropertyChanged([CallerMemberName] string n = null)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
	}
}
