using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BIN;

public class MEPLevelChangerViewModel : INotifyPropertyChanged
{
	private UIDocument _uidoc;

	private ExternalEvent _exEvent;

	private MEPLevelChangerHandler _handler;

	private Window _view;

	private bool _isSel;

	private bool _isActive = true;

	private bool _isLevel;

	public ObservableCollection<Level> Levels { get; set; }

	public ObservableCollection<MEPCategoryItem> Categories { get; set; }

	public Level SelectedNewLevel { get; set; }

	public Level SelectedSourceLevel { get; set; }

	public bool IsScopeSelection
	{
		get
		{
			return _isSel;
		}
		set
		{
			_isSel = value;
			OnPropertyChanged("IsScopeSelection");
		}
	}

	public bool IsScopeActiveView
	{
		get
		{
			return _isActive;
		}
		set
		{
			_isActive = value;
			OnPropertyChanged("IsScopeActiveView");
		}
	}

	public bool IsScopeSpecificLevel
	{
		get
		{
			return _isLevel;
		}
		set
		{
			_isLevel = value;
			OnPropertyChanged("IsScopeSpecificLevel");
		}
	}

	public ICommand RunCommand { get; }

	public ICommand SelectAllCommand { get; }

	public ICommand SelectNoneCommand { get; }

	public event PropertyChangedEventHandler PropertyChanged;

	public MEPLevelChangerViewModel(UIDocument uidoc, ExternalEvent exEvent, MEPLevelChangerHandler handler)
	{
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		_uidoc = uidoc;
		_exEvent = exEvent;
		_handler = handler;
		Levels = new ObservableCollection<Level>(from Level l in (IEnumerable)new FilteredElementCollector(uidoc.Document).OfClass(typeof(Level))
			orderby l.Elevation
			select l);
		Categories = new ObservableCollection<MEPCategoryItem>();
		InitCategories();
		RunCommand = new RelayCommand(delegate
		{
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			_handler.SetViewModel(this);
			_view.Close();
			_exEvent.Raise();
		});
		SelectAllCommand = new RelayCommand(delegate
		{
			SetAll(status: true);
		});
		SelectNoneCommand = new RelayCommand(delegate
		{
			SetAll(status: false);
		});
		SelectedNewLevel = Levels.FirstOrDefault();
		SelectedSourceLevel = Levels.FirstOrDefault();
	}

	private void SetAll(bool status)
	{
		foreach (MEPCategoryItem cat in Categories)
		{
			cat.IsChecked = status;
		}
	}

	private void InitCategories()
	{
		List<MEPCategoryItem> list = new List<MEPCategoryItem>
		{
			new MEPCategoryItem("Ducts", (BuiltInCategory)(-2008000)),
			new MEPCategoryItem("Duct Fittings", (BuiltInCategory)(-2008010)),
			new MEPCategoryItem("Duct Accessories", (BuiltInCategory)(-2008016)),
			new MEPCategoryItem("Air Terminals (Mi?ng gi\ufffd)", (BuiltInCategory)(-2008013)),
			new MEPCategoryItem("Flex Ducts", (BuiltInCategory)(-2008020)),
			new MEPCategoryItem("Mechanical Equipment", (BuiltInCategory)(-2001140)),
			new MEPCategoryItem("Pipes", (BuiltInCategory)(-2008044)),
			new MEPCategoryItem("Pipe Fittings", (BuiltInCategory)(-2008049)),
			new MEPCategoryItem("Pipe Accessories", (BuiltInCategory)(-2008055)),
			new MEPCategoryItem("Plumbing Fixtures", (BuiltInCategory)(-2001160)),
			new MEPCategoryItem("Sprinklers", (BuiltInCategory)(-2008099)),
			new MEPCategoryItem("Flex Pipes", (BuiltInCategory)(-2008050)),
			new MEPCategoryItem("Cable Trays", (BuiltInCategory)(-2008130)),
			new MEPCategoryItem("Cable Tray Fittings", (BuiltInCategory)(-2008126)),
			new MEPCategoryItem("Conduits", (BuiltInCategory)(-2008132)),
			new MEPCategoryItem("Conduit Fittings", (BuiltInCategory)(-2008128)),
			new MEPCategoryItem("Electrical Equipment (Thi?t b? di?n)", (BuiltInCategory)(-2001040)),
			new MEPCategoryItem("Electrical Fixtures", (BuiltInCategory)(-2001060)),
			new MEPCategoryItem("Lighting Fixtures", (BuiltInCategory)(-2001120)),
			new MEPCategoryItem("Data Devices", (BuiltInCategory)(-2008083)),
			new MEPCategoryItem("Fire Alarm Devices", (BuiltInCategory)(-2008085)),
			new MEPCategoryItem("Communication Devices", (BuiltInCategory)(-2008081)),
			new MEPCategoryItem("Security Devices", (BuiltInCategory)(-2008079))
		};
		foreach (MEPCategoryItem item in list.OrderBy((MEPCategoryItem x) => x.Name))
		{
			Categories.Add(item);
		}
	}

	public void SetView(Window v)
	{
		_view = v;
	}

	protected void OnPropertyChanged([CallerMemberName] string n = null)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
	}
}
