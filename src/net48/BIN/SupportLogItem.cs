using System.ComponentModel;

namespace BIN;

public class SupportLogItem : INotifyPropertyChanged
{
	public int Index { get; set; }

	public string ElementId { get; set; }

	public string TypeName { get; set; }

	public string Status { get; set; }

	public string StatusCode { get; set; }

	public string Detail { get; set; }

	public event PropertyChangedEventHandler PropertyChanged;
}
