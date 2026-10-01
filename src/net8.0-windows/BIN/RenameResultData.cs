namespace BIN;

public class RenameResultData
{
	public int TotalItems { get; set; }

	public int MatchedItems { get; set; }

	public int RenamedItems { get; set; }

	public int SkippedSameName { get; set; }

	public int SkippedConflict { get; set; }

	public int Failed { get; set; }
}
