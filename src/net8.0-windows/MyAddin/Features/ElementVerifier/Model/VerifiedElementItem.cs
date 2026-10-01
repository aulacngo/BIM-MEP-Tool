using System.ComponentModel;
using System.Runtime.CompilerServices;
using Autodesk.Revit.DB;

namespace MyAddin.Features.ElementVerifier.Model;

public class VerifiedElementItem : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler PropertyChanged;

	protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
	private int _csvId;

	private string _category = "Đang tải...";

	private string _type = "Đang tải...";

	private string _status = "Đang tải...";

	public int CsvId
	{
		get
		{
			return _csvId;
		}
		set
		{
			if (_csvId != value)
			{
				_csvId = value;
				OnPropertyChanged("CsvId");
			}
		}
	}

	public string Category
	{
		get
		{
			return _category;
		}
		set
		{
			if (_category != value)
			{
				_category = value;
				OnPropertyChanged("Category");
			}
		}
	}

	public string Type
	{
		get
		{
			return _type;
		}
		set
		{
			if (_type != value)
			{
				_type = value;
				OnPropertyChanged("Type");
			}
		}
	}

	public string Status
	{
		get
		{
			return _status;
		}
		set
		{
			if (_status != value)
			{
				_status = value;
				OnPropertyChanged("Status");
			}
		}
	}

	public ElementId RevitElementId { get; set; } = ElementId.InvalidElementId;
}
