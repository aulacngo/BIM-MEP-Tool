using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Autodesk.Revit.DB;

namespace BIN;

public class ScheduleNode : INotifyPropertyChanged
{
	private bool? _isSelected = false;

	private ScheduleNode _parent;

	private bool _isExpanded = true;

	public string Name { get; set; }

	public ViewSchedule Schedule { get; set; }

	public ObservableCollection<ScheduleNode> Children { get; set; }

	public bool? IsSelected
	{
		get
		{
			return _isSelected;
		}
		set
		{
			SetIsSelected(value, updateChildren: true, updateParent: true);
		}
	}

	public bool IsExpanded
	{
		get
		{
			return _isExpanded;
		}
		set
		{
			if (_isExpanded != value)
			{
				_isExpanded = value;
				OnPropertyChanged("IsExpanded");
			}
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public ScheduleNode()
	{
		Children = new ObservableCollection<ScheduleNode>();
		Children.CollectionChanged += delegate(object sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.NewItems != null)
			{
				foreach (ScheduleNode scheduleNode in e.NewItems)
				{
					scheduleNode._parent = this;
				}
			}
			if (e.OldItems != null)
			{
				foreach (ScheduleNode scheduleNode2 in e.OldItems)
				{
					scheduleNode2._parent = null;
				}
			}
		};
	}

	private void SetIsSelected(bool? value, bool updateChildren, bool updateParent)
	{
		if (value == _isSelected)
		{
			return;
		}
		_isSelected = value;
		if (updateChildren && _isSelected.HasValue)
		{
			foreach (ScheduleNode child in Children)
			{
				child.SetIsSelected(_isSelected, updateChildren: true, updateParent: false);
			}
		}
		if (updateParent && _parent != null)
		{
			_parent.VerifyCheckState();
		}
		OnPropertyChanged("IsSelected");
	}

	private void VerifyCheckState()
	{
		if (Children.Count == 0)
		{
			return;
		}
		bool? state = null;
		for (int i = 0; i < Children.Count; i++)
		{
			bool? current = Children[i].IsSelected;
			if (i == 0)
			{
				state = current;
			}
			else if (state != current)
			{
				state = null;
				break;
			}
		}
		SetIsSelected(state, updateChildren: false, updateParent: true);
	}

	protected void OnPropertyChanged(string name)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
