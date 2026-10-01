using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace BIN.Common.Revit;

public class WarningRecorder : IFailuresPreprocessor
{
	public List<string> CapturedWarnings { get; private set; } = new List<string>();

	public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Invalid comparison between Unknown and I4
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		IList<FailureMessageAccessor> failures = failuresAccessor.GetFailureMessages();
		if (failures.Count == 0)
		{
			return (FailureProcessingResult)0;
		}
		foreach (FailureMessageAccessor f in failures)
		{
			if ((int)f.GetSeverity() == 1)
			{
				string description = f.GetDescriptionText();
				if (!string.IsNullOrWhiteSpace(description) && !CapturedWarnings.Contains(description))
				{
					CapturedWarnings.Add(description);
				}
				failuresAccessor.DeleteWarning(f);
			}
		}
		return (FailureProcessingResult)0;
	}
}
