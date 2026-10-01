using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace BIN;

public class IgnoreWarningsPreprocessor : IFailuresPreprocessor
{
	public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Invalid comparison between Unknown and I4
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		IList<FailureMessageAccessor> failures = failuresAccessor.GetFailureMessages();
		foreach (FailureMessageAccessor failure in failures)
		{
			if ((int)failure.GetSeverity() == 1)
			{
				failuresAccessor.DeleteWarning(failure);
			}
		}
		return (FailureProcessingResult)0;
	}
}
