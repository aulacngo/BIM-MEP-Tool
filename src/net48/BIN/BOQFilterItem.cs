using System.Collections.ObjectModel;

namespace BIN;

public class BOQFilterItem
{
	public string FilterType { get; set; } = "contain";

	public ObservableCollection<BOQFilterValue> FilterValues { get; set; } = new ObservableCollection<BOQFilterValue>();
}
