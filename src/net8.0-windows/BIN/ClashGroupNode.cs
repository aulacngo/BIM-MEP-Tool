using System.Collections.ObjectModel;

namespace BIN;

public class ClashGroupNode
{
	public string GroupName { get; set; }

	public ObservableCollection<ClashResultData> Items { get; set; } = new ObservableCollection<ClashResultData>();

	public int ClashCount => Items.Count;

	public string DisplayName => $"{GroupName} ({ClashCount} clashes)";
}
