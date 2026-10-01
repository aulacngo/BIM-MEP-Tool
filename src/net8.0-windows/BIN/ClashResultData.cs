using System.ComponentModel;
using System.Runtime.CompilerServices;
using Autodesk.Revit.DB;

namespace BIN;

public class ClashResultData : INotifyPropertyChanged
{
	private string _element1_Type = "Loading...";

	private string _element2_Type = "Loading...";

	private bool _isResolved;

	public string GroupName { get; set; }

	public string Name { get; set; }

	public string Status { get; set; }

	public string GridLocation { get; set; }

	public string Element1_Id { get; set; }

	public string Element2_Id { get; set; }

	public XYZ ClashPoint { get; set; }

	public string Element1_Type
	{
		get
		{
			return _element1_Type;
		}
		set
		{
			if (_element1_Type != value)
			{
				_element1_Type = value;
				OnPropertyChanged("Element1_Type");
			}
		}
	}

	public string Element2_Type
	{
		get
		{
			return _element2_Type;
		}
		set
		{
			if (_element2_Type != value)
			{
				_element2_Type = value;
				OnPropertyChanged("Element2_Type");
			}
		}
	}

	public bool IsResolved
	{
		get
		{
			return _isResolved;
		}
		set
		{
			if (_isResolved != value)
			{
				_isResolved = value;
				OnPropertyChanged("IsResolved");
			}
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
