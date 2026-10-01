using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace BIN;

public class BOQGroupItem : INotifyPropertyChanged
{
	private string _groupName;

	private string _filterType = "contain";

	private ObservableCollection<BOQFilterValue> _filterValues = new ObservableCollection<BOQFilterValue>();

	public string GroupName
	{
		get
		{
			return _groupName;
		}
		set
		{
			_groupName = value;
			OnPropertyChanged("GroupName");
			OnPropertyChanged("FilteredGasConduitTypes");
		}
	}

	public string FilterType
	{
		get
		{
			return _filterType;
		}
		set
		{
			_filterType = value;
			OnPropertyChanged("FilterType");
		}
	}

	public ObservableCollection<BOQFilterValue> FilterValues
	{
		get
		{
			return _filterValues;
		}
		set
		{
			_filterValues = value;
			OnPropertyChanged("FilterValues");
		}
	}

	public List<string> FilteredGasConduitTypes
	{
		get
		{
			if (string.IsNullOrEmpty(GroupName))
			{
				return new List<string>();
			}
			ObservableCollection<string> source = BOQWindow.StaticHVACGasConduitTypes;
			if (source == null)
			{
				return new List<string>();
			}
			return source.Where((string x) => x.IndexOf(GroupName, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	protected void OnPropertyChanged(string propertyName)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
