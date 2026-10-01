namespace BIN;

public class InsulationTypeItem
{
	public string Name { get; set; }

	public object Tag { get; set; }

	public override string ToString()
	{
		return Name;
	}
}
