using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace BIN.Common.Revit;

public class DeleteWarningSuper : IFailuresPreprocessor
{
	public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Invalid comparison between Unknown and I4
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		IList<FailureMessageAccessor> failures = failuresAccessor.GetFailureMessages();
		foreach (FailureMessageAccessor f in failures)
		{
			if ((int)f.GetSeverity() == 1)
			{
				failuresAccessor.DeleteWarning(f);
			}
		}
		return (FailureProcessingResult)0;
	}
}
