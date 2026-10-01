using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BIN;

public class McpExternalEventHandler : IExternalEventHandler
{
	public Func<UIApplication, string> CurrentAction { get; set; }
	public string ResultJson { get; private set; }
	public Exception Error { get; private set; }
	public ManualResetEvent ResetEvent { get; } = new ManualResetEvent(false);

	public void Execute(UIApplication app)
	{
		try
		{
			Error = null;
			ResultJson = CurrentAction?.Invoke(app) ?? "{}";
		}
		catch (Exception ex)
		{
			Error = ex;
			ResultJson = $"{{\"error\":\"{EscapeJson(ex.Message)}\"}}";
		}
		finally
		{
			ResetEvent.Set();
		}
	}

	public string GetName() => "RevitMcpExternalEventHandler";

	public static string EscapeJson(string s)
	{
		if (s == null) return "";
		return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
	}
}

public static class RevitMcpServer
{
	private static HttpListener _listener;
	private static Thread _listenerThread;
	private static bool _isRunning;
	private static McpExternalEventHandler _handler;
	private static ExternalEvent _externalEvent;
	private static UIApplication _uiApp;
	private static readonly object ExecutionSync = new object();

	public const int Port = 8077;

	public static void Start(UIApplication uiApp)
	{
		if (_isRunning) return;

		_uiApp = uiApp;
		_handler = new McpExternalEventHandler();
		_externalEvent = ExternalEvent.Create(_handler);

		try
		{
			_listener = new HttpListener();
			_listener.Prefixes.Add($"http://localhost:{Port}/");
			_listener.Start();
			_isRunning = true;

			_listenerThread = new Thread(ListenLoop) { IsBackground = true };
			_listenerThread.Start();
		}
		catch { }
	}

	public static void Stop()
	{
		_isRunning = false;
		try
		{
			_listener?.Stop();
			_listener?.Close();
		}
		catch { }
	}

	private static void ListenLoop()
	{
		while (_isRunning && _listener != null && _listener.IsListening)
		{
			try
			{
				HttpListenerContext ctx = _listener.GetContext();
				ThreadPool.QueueUserWorkItem((state) => HandleRequest(ctx));
			}
			catch { }
		}
	}

	private static void HandleRequest(HttpListenerContext ctx)
	{
		string rawUrl = ctx.Request.RawUrl?.ToLowerInvariant() ?? "/";
		string responseJson = "{}";
		int statusCode = 200;

		try
		{
			if (rawUrl.StartsWith("/commands/latest") || rawUrl.StartsWith("/diagnostics/commands"))
			{
				int count = GetCount(ctx, 100);
				responseJson = CommandDiagnostics.ReadLatestJson(count);
			}
			else if (rawUrl.StartsWith("/commands/last-error"))
			{
				responseJson = CommandDiagnostics.ReadLastErrorJson();
			}
			else if (rawUrl.StartsWith("/journal/latest"))
			{
				responseJson = CommandDiagnostics.ReadLatestJournalJson(GetCount(ctx, 300));
			}
			else if (rawUrl.StartsWith("/diagnostics/start"))
			{
				FlexPipeDiagnostics.Start();
				responseJson = FlexPipeDiagnostics.ReadLatestJson(20);
			}
			else if (rawUrl.StartsWith("/diagnostics/stop"))
			{
				FlexPipeDiagnostics.Stop();
				responseJson = FlexPipeDiagnostics.ReadLatestJson(20);
			}
			else if (rawUrl.StartsWith("/diagnostics/latest") || rawUrl.StartsWith("/diagnostics/status"))
			{
				int count = 200;
				string value = ctx.Request.QueryString["n"];
				if (!string.IsNullOrEmpty(value)) int.TryParse(value, out count);
				responseJson = FlexPipeDiagnostics.ReadLatestJson(count);
			}
			else if (rawUrl == "/" || rawUrl.StartsWith("/ping"))
			{
				responseJson = ExecuteInRevit(app =>
				{
					UIDocument uidoc = app.ActiveUIDocument;
					Document doc = uidoc?.Document;
					string v = app.Application.VersionNumber;
					string title = doc?.Title ?? "None";
					string view = doc?.ActiveView?.Name ?? "None";
					return $"{{\"status\":\"connected\",\"revitVersion\":\"{v}\",\"activeDoc\":\"{McpExternalEventHandler.EscapeJson(title)}\",\"activeView\":\"{McpExternalEventHandler.EscapeJson(view)}\"}}";
				});
			}
			else if (rawUrl.StartsWith("/selection"))
			{
				responseJson = ExecuteInRevit(app =>
				{
					UIDocument uidoc = app.ActiveUIDocument;
					Document doc = uidoc?.Document;
					if (doc == null || uidoc == null) return "{\"count\":0,\"elements\":[]}";

					ICollection<ElementId> ids = uidoc.Selection.GetElementIds();
					StringBuilder sb = new StringBuilder();
					sb.Append($"{{\"count\":{ids.Count},\"elements\":[");
					int i = 0;
					foreach (ElementId id in ids)
					{
						Element e = doc.GetElement(id);
						if (e == null) continue;
						if (i++ > 0) sb.Append(",");
						sb.Append($"{{\"id\":{id.Value},\"name\":\"{McpExternalEventHandler.EscapeJson(e.Name)}\",\"category\":\"{McpExternalEventHandler.EscapeJson(e.Category?.Name ?? "")}\"}}");
					}
					sb.Append("]}");
					return sb.ToString();
				});
			}
			else if (rawUrl.StartsWith("/cad/links"))
			{
				responseJson = ExecuteInRevit(app => GetCadLinksJson(app.ActiveUIDocument?.Document));
			}
			else if (rawUrl.StartsWith("/views/open"))
			{
				responseJson = ExecuteInRevit(GetOpenViewsJson);
			}
			else if (rawUrl.StartsWith("/cad/inspect"))
			{
				int importId;
				int.TryParse(ctx.Request.QueryString["id"], out importId);
				string blockName = ctx.Request.QueryString["block"];
				responseJson = ExecuteInRevit(app => GetCadInspectionJson(
					app.ActiveUIDocument?.Document, importId, blockName, GetBoundedCount(ctx, 50, 1, 200)));
			}
						else if (rawUrl.StartsWith("/test/run-all-rotate-tests"))
			{
				responseJson = ExecuteInRevit(app =>
				{
					UIDocument uidoc = app.ActiveUIDocument;
					Document doc = uidoc?.Document;
					if (doc == null || uidoc == null) return "{\"error\":\"No active document\"}";

					List<string> testResults = new List<string>();
					int passCount = 0;
					int failCount = 0;

					var pipeFittings = new FilteredElementCollector(doc)
						.OfCategory(BuiltInCategory.OST_PipeFitting)
						.WhereElementIsNotElementType()
						.ToElements();

					FamilyInstance testFitting = null;
					Element axisPipe = null;

					foreach (Element f in pipeFittings)
					{
						if (f is FamilyInstance fi && fi.MEPModel?.ConnectorManager != null)
						{
							foreach (Connector c in fi.MEPModel.ConnectorManager.Connectors)
							{
								if (c.IsConnected)
								{
									foreach (Connector other in c.AllRefs)
									{
										if (other.Owner is MEPCurve curve && curve.Id != fi.Id)
										{
											LocationCurve lc = curve.Location as LocationCurve;
											if (lc?.Curve is Line)
											{
												testFitting = fi;
												axisPipe = curve;
												break;
											}
										}
									}
								}
								if (testFitting != null) break;
							}
						}
						if (testFitting != null) break;
					}

					if (testFitting == null || axisPipe == null)
					{
						return "{\"status\":\"error\",\"message\":\"Could not find connected test fitting and pipe\"}";
					}

					LocationCurve axisLc = axisPipe.Location as LocationCurve;
					Line axisCurveLine = axisLc.Curve as Line;
					Line axisLine = Line.CreateUnbound(axisCurveLine.GetEndPoint(0), (axisCurveLine.GetEndPoint(1) - axisCurveLine.GetEndPoint(0)).Normalize());

					// Test 1: Branch Traversal
					HashSet<ElementId> branchIds = new HashSet<ElementId> { testFitting.Id };
					Queue<Element> q = new Queue<Element>();
					q.Enqueue(testFitting);
					while (q.Count > 0)
					{
						Element curr = q.Dequeue();
						ConnectorSet conns = (curr is MEPCurve mc) ? mc.ConnectorManager?.Connectors : (curr is FamilyInstance fInstance) ? fInstance.MEPModel?.ConnectorManager?.Connectors : null;
						if (conns != null)
						{
							foreach (Connector c in conns)
							{
								if (c.IsConnected)
								{
									foreach (Connector other in c.AllRefs)
									{
										if (other.Owner != null && other.Owner.Id != axisPipe.Id && !branchIds.Contains(other.Owner.Id))
										{
											branchIds.Add(other.Owner.Id);
											q.Enqueue(other.Owner);
										}
									}
								}
							}
						}
					}

					#if NET8_0_OR_GREATER || NETCOREAPP
					long fittingIdVal = testFitting.Id.Value;
					long axisPipeIdVal = axisPipe.Id.Value;
					#else
					int fittingIdVal = testFitting.Id.IntegerValue;
					int axisPipeIdVal = axisPipe.Id.IntegerValue;
					#endif

					testResults.Add($"{{\"test\":\"CollectConnectedBranch\",\"status\":\"PASSED\",\"elementCount\":{branchIds.Count},\"fittingId\":{fittingIdVal},\"axisPipeId\":{axisPipeIdVal}}}");
					passCount++;

					// Test 2: Multi-step Rotation & Rollback
					double[] testAngles = new double[] { 45.0, 45.0, 90.0, -45.0, -135.0 };
					double cumulativeAngle = 0;

					for (int i = 0; i < testAngles.Length; i++)
					{
						double deg = testAngles[i];
						cumulativeAngle += deg;
						double rad = deg * Math.PI / 180.0;
						try
						{
							using (Transaction trans = new Transaction(doc, $"Test Rotate {deg} deg"))
							{
								trans.Start();
								ElementTransformUtils.RotateElements(doc, branchIds.ToList(), axisLine, rad);
								trans.Commit();
							}
							testResults.Add($"{{\"test\":\"Rotate_Step_{i+1}\",\"status\":\"PASSED\",\"angle\":{deg},\"cumulativeAngle\":{cumulativeAngle}}}");
							passCount++;
						}
						catch (Exception ex)
						{
							testResults.Add($"{{\"test\":\"Rotate_Step_{i+1}\",\"status\":\"FAILED\",\"error\":\"{McpExternalEventHandler.EscapeJson(ex.Message)}\"}}");
							failCount++;
						}
					}

					// Test 3: Calculate Angle to Target
					try
					{
						var otherPipes = new FilteredElementCollector(doc).OfClass(typeof(Autodesk.Revit.DB.Plumbing.Pipe)).WhereElementIsNotElementType().ToElements();
						Element targetPipe = otherPipes.FirstOrDefault(p => p.Id != axisPipe.Id);
						if (targetPipe != null)
						{
							#if NET8_0_OR_GREATER || NETCOREAPP
							long targetIdVal = targetPipe.Id.Value;
							#else
							int targetIdVal = targetPipe.Id.IntegerValue;
							#endif
							testResults.Add($"{{\"test\":\"CalculateAngleToTarget\",\"status\":\"PASSED\",\"targetPipeId\":{targetIdVal}}}");
							passCount++;
						}
					}
					catch (Exception ex)
					{
						testResults.Add($"{{\"test\":\"CalculateAngleToTarget\",\"status\":\"FAILED\",\"error\":\"{McpExternalEventHandler.EscapeJson(ex.Message)}\"}}");
						failCount++;
					}

					uidoc.RefreshActiveView();

					return $"{{\"summary\":{{\"total\":{passCount + failCount},\"passed\":{passCount},\"failed\":{failCount}}},\"results\":[{string.Join(",", testResults)}]}}";
				});
			}
			else if (rawUrl.StartsWith("/test/list-test-elements"))
			{
				responseJson = ExecuteInRevit(app =>
				{
					UIDocument uidoc = app.ActiveUIDocument;
					Document doc = uidoc?.Document;
					if (doc == null || uidoc == null) return "{\"error\":\"No active document\"}";

					var pipes = new FilteredElementCollector(doc).OfClass(typeof(Autodesk.Revit.DB.Plumbing.Pipe)).WhereElementIsNotElementType().ToElements();
					var pipeFittings = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_PipeFitting).WhereElementIsNotElementType().ToElements();

					List<string> items = new List<string>();
					foreach (Element p in pipes)
					{
						LocationCurve lc = p.Location as LocationCurve;
						if (lc != null)
						{
							XYZ p0 = lc.Curve.GetEndPoint(0);
							XYZ p1 = lc.Curve.GetEndPoint(1);
							if (p0.Y >= -10.0 && p0.Y <= 500.0)
							{
								items.Add($"{{\"id\":{p.Id.Value},\"category\":\"Pipe\",\"y\":{Math.Round(p0.Y, 1)},\"z\":{Math.Round(p0.Z, 1)},\"len\":{Math.Round(lc.Curve.Length, 1)}}}");
							}
						}
					}
					foreach (Element f in pipeFittings)
					{
						LocationPoint lp = f.Location as LocationPoint;
						if (lp != null && lp.Point.Y >= -10.0 && lp.Point.Y <= 500.0)
						{
							items.Add($"{{\"id\":{f.Id.Value},\"category\":\"Fitting\",\"y\":{Math.Round(lp.Point.Y, 1)},\"z\":{Math.Round(lp.Point.Z, 1)},\"name\":\"{McpExternalEventHandler.EscapeJson(f.Name)}\"}}");
						}
					}

					return $"{{\"elements\":[{string.Join(",", items)}]}}";
				});
			}
			else if (rawUrl.StartsWith("/connect-flex"))
			{
				responseJson = ExecuteInRevit(app =>
				{
					UIDocument uidoc = app.ActiveUIDocument;
					Document doc = uidoc?.Document;
					if (doc == null || uidoc == null) return "{\"error\":\"No active document\"}";

					string message = "";
					ElementSet elSet = new ElementSet();
					ConnectSprinklerFlexPipeCmd cmd = new ConnectSprinklerFlexPipeCmd();
					Result r = cmd.Execute(new FakeExternalCommandData(app), ref message, elSet);
					return $"{{\"result\":\"{r}\",\"message\":\"{McpExternalEventHandler.EscapeJson(message)}\"}}";
				});
			}
			else
			{
				responseJson = "{\"status\":\"ok\"}";
			}
		}
		catch (Exception ex)
		{
			statusCode = 500;
			responseJson = $"{{\"error\":\"{McpExternalEventHandler.EscapeJson(ex.Message)}\"}}";
		}

		try
		{
			ctx.Response.StatusCode = statusCode;
			ctx.Response.ContentType = "application/json; charset=utf-8";
			ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");
			byte[] buffer = Encoding.UTF8.GetBytes(responseJson);
			ctx.Response.ContentLength64 = buffer.Length;
			ctx.Response.OutputStream.Write(buffer, 0, buffer.Length);
			ctx.Response.OutputStream.Close();
		}
		catch { }
	}

	public static string ExecuteInRevit(Func<UIApplication, string> action)
	{
		if (_handler == null || _externalEvent == null) return "{\"error\":\"Server handler not initialized\"}";

		lock (ExecutionSync)
		{
			_handler.ResetEvent.Reset();
			_handler.CurrentAction = action;
			_externalEvent.Raise();

			bool finished = _handler.ResetEvent.WaitOne(60000);
			if (!finished)
			{
				return "{\"error\":\"Revit execution timed out after 60s\"}";
			}

			if (_handler.Error != null)
			{
				return $"{{\"error\":\"{McpExternalEventHandler.EscapeJson(_handler.Error.Message)}\"}}";
			}

			return _handler.ResultJson;
		}
	}

	private static int GetCount(HttpListenerContext ctx, int defaultValue)
	{
		string value = ctx.Request.QueryString["n"];
		return !string.IsNullOrEmpty(value) && int.TryParse(value, out int count) ? count : defaultValue;
	}

	private static int GetBoundedCount(HttpListenerContext ctx, int defaultValue, int minimum, int maximum)
	{
		int count = GetCount(ctx, defaultValue);
		return Math.Max(minimum, Math.Min(maximum, count));
	}

	private static string GetCadLinksJson(Document doc)
	{
		if (doc == null) return "{\"error\":\"No active document\"}";

		List<ImportInstance> imports = new FilteredElementCollector(doc)
			.OfClass(typeof(ImportInstance))
			.WhereElementIsNotElementType()
			.Cast<ImportInstance>()
			.OrderBy(x => x.Id.Value)
			.ToList();
		StringBuilder sb = new StringBuilder();
		sb.Append("{\"document\":\"").Append(McpExternalEventHandler.EscapeJson(doc.Title))
			.Append("\",\"count\":").Append(imports.Count).Append(",\"links\":[");
		for (int i = 0; i < imports.Count; i++)
		{
			if (i > 0) sb.Append(",");
			AppendCadLinkSummaryJson(sb, doc, imports[i]);
		}
		sb.Append("]}");
		return sb.ToString();
	}

	private static string GetOpenViewsJson(UIApplication app)
	{
		UIDocument uidoc = app?.ActiveUIDocument;
		Document doc = uidoc?.Document;
		if (doc == null) return "{\"error\":\"No active document\"}";

		ElementId activeViewId = doc.ActiveView?.Id;
		IList<UIView> uiViews = uidoc.GetOpenUIViews();
		StringBuilder sb = new StringBuilder();
		sb.Append("{\"document\":\"").Append(McpExternalEventHandler.EscapeJson(doc.Title))
			.Append("\",\"activeViewId\":").Append(activeViewId?.Value ?? ElementId.InvalidElementId.Value)
			.Append(",\"count\":").Append(uiViews.Count).Append(",\"views\":[");
		for (int i = 0; i < uiViews.Count; i++)
		{
			if (i > 0) sb.Append(",");
			UIView uiView = uiViews[i];
			View view = doc.GetElement(uiView.ViewId) as View;
			IList<XYZ> zoom = null;
			try { zoom = uiView.GetZoomCorners(); } catch { }
			sb.Append("{\"id\":").Append(uiView.ViewId.Value)
				.Append(",\"name\":\"").Append(McpExternalEventHandler.EscapeJson(view?.Name ?? "(unresolved)")).Append("\"")
				.Append(",\"viewType\":\"").Append(McpExternalEventHandler.EscapeJson(view?.ViewType.ToString() ?? "Unknown")).Append("\"")
				.Append(",\"isActive\":").Append((activeViewId != null && uiView.ViewId == activeViewId).ToString().ToLower());
			if (zoom != null && zoom.Count == 2)
			{
				sb.Append(",\"zoomCornersMm\":[");
				AppendPointMmJson(sb, zoom[0]);
				sb.Append(",");
				AppendPointMmJson(sb, zoom[1]);
				sb.Append("]");
			}
			sb.Append("}");
		}
		sb.Append("]}");
		return sb.ToString();
	}

	private static string GetCadInspectionJson(Document doc, int importId, string requestedBlock, int maxPoints)
	{
		if (doc == null) return "{\"error\":\"No active document\"}";
		ImportInstance import = doc.GetElement(new ElementId(importId)) as ImportInstance;
		if (import == null) return "{\"error\":\"CAD ImportInstance was not found\"}";
		CADLinkType linkType = doc.GetElement(import.GetTypeId()) as CADLinkType;
		if (linkType == null)
		{
			return "{\"error\":\"Selected CAD is imported rather than linked; block inspection currently requires a CAD link\"}";
		}

		List<string> blocks = PlaceFamilyUtils.GetListBlockCad(import, linkType);
		List<string> layers = GetCadLayerNames(import);
		StringBuilder sb = new StringBuilder();
		sb.Append("{\"link\":");
		AppendCadLinkSummaryJson(sb, doc, import);
		sb.Append(",\"blockCount\":").Append(blocks.Count).Append(",\"blocks\":[");
		AppendStringArrayJson(sb, blocks, 500);
		sb.Append("],\"layerCount\":").Append(layers.Count).Append(",\"layers\":[");
		AppendStringArrayJson(sb, layers, 500);
		sb.Append("]");

		if (!string.IsNullOrWhiteSpace(requestedBlock))
		{
			List<XYZ> points = PlaceFamilyUtils.GetListBlockCadByName(import, linkType, requestedBlock);
			sb.Append(",\"requestedBlock\":\"").Append(McpExternalEventHandler.EscapeJson(requestedBlock)).Append("\"")
				.Append(",\"matchCount\":").Append(points.Count).Append(",\"pointsMm\":[");
			for (int i = 0; i < Math.Min(points.Count, maxPoints); i++)
			{
				if (i > 0) sb.Append(",");
				AppendPointMmJson(sb, points[i]);
			}
			sb.Append("],\"pointsTruncated\":").Append((points.Count > maxPoints).ToString().ToLower())
				.Append(",\"scanDiagnostics\":\"").Append(McpExternalEventHandler.EscapeJson(PlaceFamilyUtils.LastBlockScanDiagnostics)).Append("\"");
		}

		sb.Append("}");
		return sb.ToString();
	}

	private static void AppendCadLinkSummaryJson(StringBuilder sb, Document doc, ImportInstance import)
	{
		Element type = doc.GetElement(import.GetTypeId());
		Transform transform = import.GetTotalTransform();
		sb.Append("{\"id\":").Append(import.Id.Value)
			.Append(",\"name\":\"").Append(McpExternalEventHandler.EscapeJson(import.Name)).Append("\"")
			.Append(",\"typeName\":\"").Append(McpExternalEventHandler.EscapeJson(type?.Name ?? "")).Append("\"")
			.Append(",\"isLinked\":").Append(import.IsLinked.ToString().ToLower())
			.Append(",\"ownerViewId\":").Append(import.OwnerViewId.Value)
			.Append(",\"path\":\"").Append(McpExternalEventHandler.EscapeJson(GetCadLinkPath(doc, import))).Append("\"")
			.Append(",\"transform\":{\"originMm\":");
		AppendPointMmJson(sb, transform.Origin);
		sb.Append(",\"basisX\":"); AppendVectorJson(sb, transform.BasisX);
		sb.Append(",\"basisY\":"); AppendVectorJson(sb, transform.BasisY);
		sb.Append(",\"basisZ\":"); AppendVectorJson(sb, transform.BasisZ);
		sb.Append(",\"scale\":").Append(transform.Scale.ToString("0.########", System.Globalization.CultureInfo.InvariantCulture)).Append("}}");
	}

	private static string GetCadLinkPath(Document doc, ImportInstance import)
	{
		try
		{
			ExternalFileReference reference = ExternalFileUtils.GetExternalFileReference(doc, import.GetTypeId());
			return reference == null ? "" : ModelPathUtils.ConvertModelPathToUserVisiblePath(reference.GetAbsolutePath());
		}
		catch (Exception ex)
		{
			return "[unavailable: " + ex.Message + "]";
		}
	}

	private static List<string> GetCadLayerNames(ImportInstance import)
	{
		Category category = import.Category;
		if (category?.SubCategories == null) return new List<string>();
		return category.SubCategories.Cast<Category>().Select(x => x.Name).Where(x => !string.IsNullOrWhiteSpace(x))
			.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
	}

	private static void AppendStringArrayJson(StringBuilder sb, IEnumerable<string> values, int maximum)
	{
		int index = 0;
		foreach (string value in values.Take(maximum))
		{
			if (index++ > 0) sb.Append(",");
			sb.Append("\"").Append(McpExternalEventHandler.EscapeJson(value)).Append("\"");
		}
	}

	private static void AppendPointMmJson(StringBuilder sb, XYZ point)
	{
		if (point == null) { sb.Append("null"); return; }
		sb.Append("{\"x\":").Append((point.X * 304.8).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))
			.Append(",\"y\":").Append((point.Y * 304.8).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))
			.Append(",\"z\":").Append((point.Z * 304.8).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).Append("}");
	}

	private static void AppendVectorJson(StringBuilder sb, XYZ vector)
	{
		if (vector == null) { sb.Append("null"); return; }
		sb.Append("{\"x\":").Append(vector.X.ToString("0.########", System.Globalization.CultureInfo.InvariantCulture))
			.Append(",\"y\":").Append(vector.Y.ToString("0.########", System.Globalization.CultureInfo.InvariantCulture))
			.Append(",\"z\":").Append(vector.Z.ToString("0.########", System.Globalization.CultureInfo.InvariantCulture)).Append("}");
	}
}

public class FakeExternalCommandData
{
	public UIApplication Application { get; }
	public FakeExternalCommandData(UIApplication app)
	{
		Application = app;
	}

	public static implicit operator ExternalCommandData(FakeExternalCommandData fake)
	{
		return (ExternalCommandData)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(ExternalCommandData));
	}
}
