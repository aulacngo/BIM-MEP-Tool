using Visibility = System.Windows.Visibility;
using System.ComponentModel;
using System.Windows;
using Autodesk.Revit.DB;

namespace BIN;

public class SystemCompareItem : INotifyPropertyChanged
{
	private string _projectSystemTypeName;

	private string _projectAbbreviation;

	private string _projectServiceType;

	private string _templateSystemTypeName;

	private string _templateAbbreviation;

	private string _templateServiceType;

	private string _projectCableTrayType;

	private string _templateCableTrayType;

	private bool _isMatch;

	public Element ElementType { get; set; }

	public string ProjectSystemTypeName
	{
		get
		{
			return _projectSystemTypeName;
		}
		set
		{
			_projectSystemTypeName = value;
			OnPropertyChanged("ProjectSystemTypeName");
		}
	}

	public string ProjectAbbreviation
	{
		get
		{
			return _projectAbbreviation;
		}
		set
		{
			_projectAbbreviation = value;
			OnPropertyChanged("ProjectAbbreviation");
		}
	}

	public string ProjectServiceType
	{
		get
		{
			return _projectServiceType;
		}
		set
		{
			_projectServiceType = value;
			OnPropertyChanged("ProjectServiceType");
		}
	}

	public string TemplateSystemTypeName
	{
		get
		{
			return _templateSystemTypeName;
		}
		set
		{
			_templateSystemTypeName = value;
			OnPropertyChanged("TemplateSystemTypeName");
			CheckMatch();
		}
	}

	public string TemplateAbbreviation
	{
		get
		{
			return _templateAbbreviation;
		}
		set
		{
			_templateAbbreviation = value;
			OnPropertyChanged("TemplateAbbreviation");
			CheckMatch();
		}
	}

	public string TemplateServiceType
	{
		get
		{
			return _templateServiceType;
		}
		set
		{
			_templateServiceType = value;
			OnPropertyChanged("TemplateServiceType");
			CheckMatch();
		}
	}

	public string ProjectCableTrayType
	{
		get
		{
			return _projectCableTrayType;
		}
		set
		{
			_projectCableTrayType = value;
			OnPropertyChanged("ProjectCableTrayType");
		}
	}

	public string TemplateCableTrayType
	{
		get
		{
			return _templateCableTrayType;
		}
		set
		{
			_templateCableTrayType = value;
			OnPropertyChanged("TemplateCableTrayType");
			CheckMatch();
		}
	}

	public bool IsMatch
	{
		get
		{
			return _isMatch;
		}
		set
		{
			_isMatch = value;
			RefreshState();
		}
	}

	public bool IsUnmapped => string.IsNullOrEmpty(TemplateSystemTypeName) && string.IsNullOrEmpty(TemplateCableTrayType) && ElementType != null;

	public string StatusText => IsUnmapped ? "" : (IsMatch ? "✔ Match" : "✖ Not Match");

	public string StatusColor => IsMatch ? "Green" : "Red";

	public bool CanChange => !IsMatch && !IsUnmapped;

	public Visibility ChangeVisibility => (IsUnmapped || ElementType == null) ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;

	public Visibility ChangeToVisibility => (!IsUnmapped) ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;

	public event PropertyChangedEventHandler PropertyChanged;

	protected void OnPropertyChanged(string name)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}

	private void RefreshState()
	{
		OnPropertyChanged("IsMatch");
		OnPropertyChanged("IsUnmapped");
		OnPropertyChanged("StatusText");
		OnPropertyChanged("StatusColor");
		OnPropertyChanged("CanChange");
		OnPropertyChanged("ChangeVisibility");
		OnPropertyChanged("ChangeToVisibility");
	}

	public void CheckMatch()
	{
		if (string.IsNullOrEmpty(TemplateSystemTypeName) && string.IsNullOrEmpty(TemplateCableTrayType))
		{
			_isMatch = true;
			RefreshState();
			return;
		}
		bool match = true;
		string pSys = ProjectSystemTypeName ?? "";
		string tSys = TemplateSystemTypeName ?? "";
		string pCb = ProjectCableTrayType ?? "";
		string tCb = TemplateCableTrayType ?? "";
		if (pSys != tSys)
		{
			match = false;
		}
		if (pCb != tCb)
		{
			match = false;
		}
		string pAbbr = ProjectAbbreviation ?? "";
		string pSvc = ProjectServiceType ?? "";
		string tAbbr = TemplateAbbreviation ?? "";
		string tSvc = TemplateServiceType ?? "";
		if (pAbbr != tAbbr)
		{
			match = false;
		}
		if (pSvc != tSvc)
		{
			match = false;
		}
		_isMatch = match;
		RefreshState();
	}
}
