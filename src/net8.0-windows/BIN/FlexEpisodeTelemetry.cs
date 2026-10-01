using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;

namespace BIN;

// Holds only element IDs while the connection command is executing.  API values are
// read after the final transaction commits; no Autodesk API object crosses the worker boundary.
internal sealed class FlexEpisodeConnection
{
	internal ElementId TeeId;
	internal ElementId ElbowId;
	internal ElementId ReducerId;
	internal ElementId FlexPipeId;
	internal ElementId SprinklerId;
}

internal static class FlexEpisodeTelemetry
{
	internal static void CaptureAndQueue(Document doc, Pipe mainPipe,
		IList<FlexEpisodeConnection> completedConnections)
	{
		if (doc == null || mainPipe == null || completedConnections == null || completedConnections.Count == 0)
		{
			return;
		}

		try
		{
			// This snapshot intentionally runs on the Revit command thread immediately after
			// tFlex.Commit().  The queued callback below receives a primitive-only DTO.
			FlexEpisodePayload episode = Capture(doc, mainPipe, completedConnections);
			QueueStandardizedEpisode(episode, completedConnections);
		}
		catch
		{
			// Observability must never change the command result.
		}
	}

	private static void QueueStandardizedEpisode(FlexEpisodePayload episode,
		IList<FlexEpisodeConnection> completedConnections)
	{
		int teeCount = completedConnections.Count(connection => IsValidElementId(connection.TeeId));
		int elbowCount = completedConnections.Count(connection => IsValidElementId(connection.ElbowId));
		string branchStatus = teeCount > 0 ? "tee_branch" : elbowCount > 0 ? "elbow_branch" : "direct_branch";
		CommandDiagnostics.WriteL2("ConnectSprinklerFlexPipe", "succeeded", "COMPLETED", new
		{
			event_kind = "sprinkler_flex_completion",
			pipe_count = 1,
			sprinkler_count = completedConnections.Count,
			branch_status = branchStatus,
			tee_count = teeCount,
			elbow_count = elbowCount,
			flex_pipe_count = episode.flex_pipe == null ? 0 : episode.flex_pipe.Count,
			all_flexes_have_three_control_points = episode.validation != null && episode.validation.all_flexes_have_three_control_points,
			endpoints_connected = episode.validation != null && episode.validation.both_ends_connected
		});
	}

	private static FlexEpisodePayload Capture(Document doc, Pipe mainPipe,
		IList<FlexEpisodeConnection> completedConnections)
	{
		FlexEpisodePayload payload = new FlexEpisodePayload
		{
			command_name = "ConnectSprinklerFlexPipe: Episode",
			occurred_utc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
			system = CaptureSystem(doc, mainPipe),
			main_pipe_size = CaptureMainPipeSize(mainPipe),
			fittings_created = new List<FlexFittingPayload>(),
			flex_pipe = new List<FlexPipePayload>(),
			sprinkler = new List<FlexSprinklerPayload>()
		};

		HashSet<ElementId> capturedFittingIds = new HashSet<ElementId>();
		foreach (FlexEpisodeConnection connection in completedConnections)
		{
			AddFitting(doc, payload.fittings_created, capturedFittingIds, connection.TeeId, "Tee");
			AddFitting(doc, payload.fittings_created, capturedFittingIds, connection.ElbowId, "Elbow");
			AddFitting(doc, payload.fittings_created, capturedFittingIds, connection.ReducerId, "Reducer");

			FlexPipe flexPipe = doc.GetElement(connection.FlexPipeId) as FlexPipe;
			if (flexPipe != null)
			{
				payload.flex_pipe.Add(CaptureFlexPipe(flexPipe));
			}

			FamilyInstance sprinkler = doc.GetElement(connection.SprinklerId) as FamilyInstance;
			if (sprinkler != null)
			{
				payload.sprinkler.Add(CaptureSprinkler(sprinkler));
			}
		}

		bool bothEndsConnected = payload.flex_pipe.Count > 0 &&
			payload.flex_pipe.All(item => item.both_ends_connected);
		int controlPointCount = payload.flex_pipe.Count == 0
			? 0
			: payload.flex_pipe.Min(item => item.control_points);
		payload.validation = new FlexValidationPayload
		{
			both_ends_connected = bothEndsConnected,
			control_points = controlPointCount,
			all_flexes_have_three_control_points = payload.flex_pipe.Count > 0 &&
				payload.flex_pipe.All(item => item.control_points == 3)
		};
		return payload;
	}

	private static FlexSystemPayload CaptureSystem(Document doc, Pipe mainPipe)
	{
		Parameter systemTypeParameter = mainPipe.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM);
		PipingSystemType systemType = systemTypeParameter == null ||
			systemTypeParameter.StorageType != StorageType.ElementId
			? null
			: doc.GetElement(systemTypeParameter.AsElementId()) as PipingSystemType;
		string displayedName = GetParameterDisplay(systemTypeParameter);
		return new FlexSystemPayload
		{
			name = string.IsNullOrWhiteSpace(displayedName) ? systemType?.Name : displayedName,
			type = systemType?.Name ?? displayedName,
			classification = systemType == null ? null : systemType.SystemClassification.ToString()
		};
	}

	private static FlexMainPipeSizePayload CaptureMainPipeSize(Pipe mainPipe)
	{
		double millimeters = GetDiameterMillimeters(mainPipe);
		return new FlexMainPipeSizePayload
		{
			millimeters = millimeters,
			canonical = CanonicalDiameter(millimeters)
		};
	}

	private static void AddFitting(Document doc, List<FlexFittingPayload> fittings,
		HashSet<ElementId> capturedIds, ElementId fittingId, string expectedPartType)
	{
		if (!IsValidElementId(fittingId) || !capturedIds.Add(fittingId)) return;
		FamilyInstance fitting = doc.GetElement(fittingId) as FamilyInstance;
		if (fitting == null) return;
		fittings.Add(new FlexFittingPayload
		{
			part_type = GetPartType(fitting, expectedPartType),
			family_name = fitting.Symbol?.Family?.Name,
			canonical_size = GetCanonicalFittingSize(fitting)
		});
	}

	private static FlexPipePayload CaptureFlexPipe(FlexPipe flexPipe)
	{
		List<XYZ> points = flexPipe.Points == null ? new List<XYZ>() : flexPipe.Points.ToList();
		double developedLength = GetLengthMillimeters(flexPipe);
		if (developedLength <= 0.0)
		{
			developedLength = GetPolylineLengthMillimeters(points);
		}
		double chordLength = points.Count < 2
			? 0.0
			: points[0].DistanceTo(points[points.Count - 1]) * 304.8;
		List<Connector> endConnectors = GetPipingEndConnectors(flexPipe);
		return new FlexPipePayload
		{
			diameter_mm = GetDiameterMillimeters(flexPipe),
			developed_length_mm = developedLength,
			chord_length_mm = chordLength,
			control_points = points.Count,
			both_ends_connected = endConnectors.Count >= 2 && endConnectors.All(connector => connector.IsConnected)
		};
	}

	private static FlexSprinklerPayload CaptureSprinkler(FamilyInstance sprinkler)
	{
		Parameter kFactor = sprinkler.get_Parameter(BuiltInParameter.RBS_FP_SPRINKLER_K_FACTOR_PARAM);
		Connector connector = GetFirstPipingConnector(sprinkler);
		return new FlexSprinklerPayload
		{
			family_name = sprinkler.Symbol?.Family?.Name,
			type_name = sprinkler.Symbol?.Name,
			k_factor = GetParameterDisplay(kFactor),
			connection_thread_mm = connector == null ? 0.0 : connector.Radius * 2.0 * 304.8
		};
	}

	private static string GetPartType(FamilyInstance fitting, string fallback)
	{
		Parameter partType = fitting.Symbol?.Family?.get_Parameter(BuiltInParameter.FAMILY_CONTENT_PART_TYPE);
		if (partType == null || partType.StorageType != StorageType.Integer) return fallback;
		return ((PartType)partType.AsInteger()).ToString();
	}

	private static string GetCanonicalFittingSize(FamilyInstance fitting)
	{
		List<Connector> connectors = GetPipingEndConnectors(fitting);
		if (connectors.Count == 0) return null;

		if (connectors.Count == 3)
		{
			int runA = 0;
			int runB = 1;
			double mostOppositeDot = double.PositiveInfinity;
			for (int first = 0; first < connectors.Count; first++)
			{
				for (int second = first + 1; second < connectors.Count; second++)
				{
					double dot = GetDirectionDot(connectors[first], connectors[second]);
					if (dot < mostOppositeDot)
					{
						mostOppositeDot = dot;
						runA = first;
						runB = second;
					}
				}
			}
			int branch = Enumerable.Range(0, connectors.Count).First(index => index != runA && index != runB);
			return string.Join("x", new[]
			{
				FormatMillimeters(GetConnectorDiameterMillimeters(connectors[runA])),
				FormatMillimeters(GetConnectorDiameterMillimeters(connectors[runB])),
				FormatMillimeters(GetConnectorDiameterMillimeters(connectors[branch]))
			}) + " mm";
		}

		return string.Join("x", connectors
			.Select(GetConnectorDiameterMillimeters)
			.OrderByDescending(size => size)
			.Select(FormatMillimeters)) + " mm";
	}

	private static double GetDirectionDot(Connector first, Connector second)
	{
		XYZ firstDirection = first.CoordinateSystem?.BasisZ;
		XYZ secondDirection = second.CoordinateSystem?.BasisZ;
		if (firstDirection == null || secondDirection == null ||
			firstDirection.GetLength() < 1e-9 || secondDirection.GetLength() < 1e-9)
		{
			return 1.0;
		}
		return firstDirection.Normalize().DotProduct(secondDirection.Normalize());
	}

	private static List<Connector> GetPipingEndConnectors(Element element)
	{
		ConnectorSet connectorSet = null;
		if (element is MEPCurve curve)
		{
			connectorSet = curve.ConnectorManager?.Connectors;
		}
		else if (element is FamilyInstance familyInstance)
		{
			connectorSet = familyInstance.MEPModel?.ConnectorManager?.Connectors;
		}

		List<Connector> connectors = new List<Connector>();
		if (connectorSet == null) return connectors;
		foreach (Connector connector in connectorSet)
		{
			if (connector.Domain == Domain.DomainPiping && connector.ConnectorType == ConnectorType.End)
			{
				connectors.Add(connector);
			}
		}
		return connectors;
	}

	private static Connector GetFirstPipingConnector(FamilyInstance familyInstance)
	{
		return GetPipingEndConnectors(familyInstance).FirstOrDefault();
	}

	private static double GetDiameterMillimeters(Element element)
	{
		Parameter diameter = element?.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
		return diameter == null || diameter.StorageType != StorageType.Double
			? 0.0
			: diameter.AsDouble() * 304.8;
	}

	private static double GetConnectorDiameterMillimeters(Connector connector)
	{
		return connector == null ? 0.0 : connector.Radius * 2.0 * 304.8;
	}

	private static double GetLengthMillimeters(Element element)
	{
		Parameter length = element?.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
		return length == null || length.StorageType != StorageType.Double
			? 0.0
			: length.AsDouble() * 304.8;
	}

	private static double GetPolylineLengthMillimeters(IList<XYZ> points)
	{
		double length = 0.0;
		for (int index = 1; index < points.Count; index++)
		{
			length += points[index - 1].DistanceTo(points[index]) * 304.8;
		}
		return length;
	}

	private static string GetParameterDisplay(Parameter parameter)
	{
		if (parameter == null) return null;
		return parameter.AsValueString() ?? parameter.AsString();
	}

	private static string CanonicalDiameter(double millimeters)
	{
		return millimeters <= 0.0 ? null : FormatMillimeters(millimeters) + " mm (DN" +
			FormatMillimeters(millimeters) + ")";
	}

	private static string FormatMillimeters(double value)
	{
		return Math.Round(value, 1, MidpointRounding.AwayFromZero)
			.ToString("0.#", CultureInfo.InvariantCulture);
	}

	private static bool IsValidElementId(ElementId id)
	{
		return id != null && id != ElementId.InvalidElementId;
	}

	private sealed class FlexEpisodePayload
	{
		public string command_name { get; set; }
		public string occurred_utc { get; set; }
		public FlexSystemPayload system { get; set; }
		public FlexMainPipeSizePayload main_pipe_size { get; set; }
		public List<FlexFittingPayload> fittings_created { get; set; }
		public List<FlexPipePayload> flex_pipe { get; set; }
		public List<FlexSprinklerPayload> sprinkler { get; set; }
		public FlexValidationPayload validation { get; set; }
	}

	private sealed class FlexSystemPayload
	{
		public string name { get; set; }
		public string type { get; set; }
		public string classification { get; set; }
	}

	private sealed class FlexMainPipeSizePayload
	{
		public double millimeters { get; set; }
		public string canonical { get; set; }
	}

	private sealed class FlexFittingPayload
	{
		public string part_type { get; set; }
		public string family_name { get; set; }
		public string canonical_size { get; set; }
	}

	private sealed class FlexPipePayload
	{
		public double diameter_mm { get; set; }
		public double developed_length_mm { get; set; }
		public double chord_length_mm { get; set; }
		public int control_points { get; set; }
		public bool both_ends_connected { get; set; }
	}

	private sealed class FlexSprinklerPayload
	{
		public string family_name { get; set; }
		public string type_name { get; set; }
		public string k_factor { get; set; }
		public double connection_thread_mm { get; set; }
	}

	private sealed class FlexValidationPayload
	{
		public bool both_ends_connected { get; set; }
		public int control_points { get; set; }
		public bool all_flexes_have_three_control_points { get; set; }
	}
}
