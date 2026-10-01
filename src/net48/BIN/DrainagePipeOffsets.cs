namespace BIN;

public static class DrainagePipeOffsets
{
	private const double MmToFt = 0.0032808398950131233;

	public static double? OffsetElbowFt { get; set; }

	public static double? OffsetYFt { get; set; }

	public static double? OffsetHFt { get; set; }

	public static double? OffsetZFt { get; set; }

	public static void SetFromMm(double? offsetElbowMm, double? offsetYMm, double? offsetHMm, double? offsetZMm)
	{
		OffsetElbowFt = (offsetElbowMm.HasValue ? new double?(offsetElbowMm.Value * 0.0032808398950131233) : ((double?)null));
		OffsetYFt = (offsetYMm.HasValue ? new double?(offsetYMm.Value * 0.0032808398950131233) : ((double?)null));
		OffsetHFt = (offsetHMm.HasValue ? new double?(offsetHMm.Value * 0.0032808398950131233) : ((double?)null));
		OffsetZFt = (offsetZMm.HasValue ? new double?(offsetZMm.Value * 0.0032808398950131233) : ((double?)null));
	}

	public static double GetOffsetElbowOrDefault(double defaultMm)
	{
		return OffsetElbowFt ?? (defaultMm / 304.8);
	}

	public static double GetOffsetYOrDefault(double defaultMm)
	{
		return OffsetYFt ?? (defaultMm / 304.8);
	}

	public static double GetOffsetHOrDefault(double defaultMm)
	{
		return OffsetHFt ?? (defaultMm / 304.8);
	}

	public static double GetOffsetZOrDefault(double defaultMm)
	{
		return OffsetZFt ?? (defaultMm / 304.8);
	}
}
