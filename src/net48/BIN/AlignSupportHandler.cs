using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

public class AlignSupportHandler : IExternalEventHandler
{
	private const double HalfSize = 1.6404199475065615;

	public List<ElementId> SelectedSymbolIds { get; set; } = new List<ElementId>();

	public bool UseSelectionMode { get; set; } = false;

	public ElementId ElementToSelect { get; set; }

	public Action<List<SupportLogItem>> OnCompleteLogs { get; set; }

	public void Execute(UIApplication uiapp)
	{
		//IL_0974: Unknown result type (might be due to invalid IL or missing references)
		//IL_020c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0245: Unknown result type (might be due to invalid IL or missing references)
		//IL_094c: Unknown result type (might be due to invalid IL or missing references)
		//IL_05f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_05ff: Expected O, but got Unknown
		//IL_0740: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_047b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0482: Expected O, but got Unknown
		//IL_0485: Unknown result type (might be due to invalid IL or missing references)
		UIDocument uidoc = uiapp.ActiveUIDocument;
		Document doc = uidoc.Document;
		View activeView = doc.ActiveView;
		if (ElementToSelect != (ElementId)null)
		{
			try
			{
				uidoc.Selection.SetElementIds((ICollection<ElementId>)new List<ElementId> { ElementToSelect });
				uidoc.ShowElements(ElementToSelect);
			}
			catch
			{
			}
			ElementToSelect = null;
			return;
		}
		List<SupportLogItem> logs = new List<SupportLogItem>();
		try
		{
			HashSet<ElementId> selectedSymbolIds = new HashSet<ElementId>(SelectedSymbolIds);
			List<FamilyInstance> supports = new List<FamilyInstance>();
			List<Pipe> pipesToSearch = new List<Pipe>();
			if (UseSelectionMode)
			{
				IList<Element> picked;
				try
				{
					picked = uidoc.Selection.PickElementsByRectangle((ISelectionFilter)(object)new AlignSupportSelectionFilter(selectedSymbolIds), "Quét chọn vùng chứa Support và Pipe cần căn chỉnh");
				}
				catch (Autodesk.Revit.Exceptions.OperationCanceledException)
				{
					OnCompleteLogs?.Invoke(null);
					return;
				}
				foreach (Element elem in picked)
				{
					FamilyInstance fi2 = (FamilyInstance)(object)((elem is FamilyInstance) ? elem : null);
					if (fi2 != null && fi2.Symbol != null && selectedSymbolIds.Contains(((Element)fi2.Symbol).Id))
					{
						supports.Add(fi2);
						continue;
					}
					Pipe pipe = (Pipe)(object)((elem is Pipe) ? elem : null);
					if (pipe != null)
					{
						Location location = ((Element)pipe).Location;
						LocationCurve lc = (LocationCurve)(object)((location is LocationCurve) ? location : null);
						if (lc != null && lc.Curve is Line)
						{
							pipesToSearch.Add(pipe);
						}
					}
				}
				if (pipesToSearch.Count == 0)
				{
					pipesToSearch = ((IEnumerable)new FilteredElementCollector(doc, ((Element)activeView).Id).OfClass(typeof(Pipe))).Cast<Pipe>().Where(delegate(Pipe p)
					{
						Location location2 = ((Element)p).Location;
						LocationCurve val2 = (LocationCurve)(object)((location2 is LocationCurve) ? location2 : null);
						return val2 != null && val2.Curve is Line;
					}).ToList();
				}
			}
			else
			{
				supports = (from FamilyInstance fi in (IEnumerable)new FilteredElementCollector(doc, ((Element)activeView).Id).OfClass(typeof(FamilyInstance))
					where fi.Symbol != null && selectedSymbolIds.Contains(((Element)fi.Symbol).Id)
					select fi).ToList();
				pipesToSearch = ((IEnumerable)new FilteredElementCollector(doc, ((Element)activeView).Id).OfClass(typeof(Pipe))).Cast<Pipe>().Where(delegate(Pipe p)
				{
					Location location3 = ((Element)p).Location;
					LocationCurve val3 = (LocationCurve)(object)((location3 is LocationCurve) ? location3 : null);
					return val3 != null && val3.Curve is Line;
				}).ToList();
			}
			List<Pipe> filteredPipes = new List<Pipe>();
			foreach (Pipe pipe2 in pipesToSearch)
			{
				Location location4 = ((Element)pipe2).Location;
				LocationCurve lc2 = (LocationCurve)(object)((location4 is LocationCurve) ? location4 : null);
				if (lc2 == null)
				{
					continue;
				}
				Curve curve = lc2.Curve;
				Line line = (Line)(object)((curve is Line) ? curve : null);
				if (line != null)
				{
					XYZ p2 = ((Curve)line).GetEndPoint(0);
					XYZ p3 = ((Curve)line).GetEndPoint(1);
					XYZ dir = (p3 - p2).Normalize();
					double vertical = Math.Abs(dir.Z);
					double horizontal = Math.Sqrt(dir.X * dir.X + dir.Y * dir.Y);
					double slope = ((horizontal > 1E-06) ? (vertical / horizontal) : double.MaxValue);
					if (!(slope >= 0.05))
					{
						filteredPipes.Add(pipe2);
					}
				}
			}
			pipesToSearch = filteredPipes;
			if (supports.Count == 0)
			{
				OnCompleteLogs?.Invoke(logs);
				return;
			}
			if (pipesToSearch.Count == 0)
			{
				foreach (FamilyInstance support in supports)
				{
					logs.Add(new SupportLogItem
					{
						ElementId = ((object)((Element)support).Id).ToString(),
						TypeName = ((Element)support.Symbol).Name,
						Status = "Bỏ qua",
						StatusCode = "skipped",
						Detail = "Không tìm thấy bất kỳ ống (Pipe) nào trong phạm vi xử lý."
					});
				}
				OnCompleteLogs?.Invoke(logs);
				return;
			}
			Transaction tx = new Transaction(doc, "Align Support");
			try
			{
				tx.Start();
				foreach (FamilyInstance support2 in supports)
				{
					string detailLog = "";
					Location location5 = ((Element)support2).Location;
					LocationPoint lp = (LocationPoint)(object)((location5 is LocationPoint) ? location5 : null);
					if (lp == null)
					{
						logs.Add(new SupportLogItem
						{
							ElementId = ((object)((Element)support2).Id).ToString(),
							TypeName = ((Element)support2.Symbol).Name,
							Status = "Bỏ qua",
							StatusCode = "skipped",
							Detail = "Support không có LocationPoint."
						});
						continue;
					}
					XYZ origin = lp.Point;
					XYZ bestPoint = null;
					string methodUsed = "PipeDirection";
					bestPoint = FindIntersectionWithPipes(pipesToSearch, origin, 1.6404199475065615, ref detailLog);
					if (bestPoint == null)
					{
						logs.Add(new SupportLogItem
						{
							ElementId = ((object)((Element)support2).Id).ToString(),
							TypeName = ((Element)support2.Symbol).Name,
							Status = "Bỏ qua",
							StatusCode = "skipped",
							Detail = "Không tìm thấy ống phù hợp cắt qua Plane 1000x1000mm. Chi tiết: " + detailLog
						});
						continue;
					}
					string paramDiagnostics = GetElevationParametersDiagnostics(support2);
					double rodLength = GetRodLength(support2);
					XYZ targetPoint = bestPoint;
					if (IsOriginAtTop(support2, rodLength))
					{
						targetPoint = new XYZ(bestPoint.X, bestPoint.Y, bestPoint.Z + rodLength);
						methodUsed += " (Origin At Top)";
					}
					XYZ delta = targetPoint - origin;
					detailLog += $" [Chẩn đoán Tọa độ: Origin Z={origin.Z * 304.8:F1}mm, Target Z={targetPoint.Z * 304.8:F1}mm (Pipe Z={bestPoint.Z * 304.8:F1}mm), Delta Z={delta.Z * 304.8:F1}mm. Params: {paramDiagnostics}]";
					if (delta.IsAlmostEqualTo(XYZ.Zero, 1E-05))
					{
						logs.Add(new SupportLogItem
						{
							ElementId = ((object)((Element)support2).Id).ToString(),
							TypeName = ((Element)support2.Symbol).Name,
							Status = "Đã đúng vị trí",
							StatusCode = "already",
							Detail = "Support đã nằm trùng khớp với centerline của ống (Sai lệch < 0.01 mm). Phương pháp: " + methodUsed + ". " + detailLog
						});
						continue;
					}
					try
					{
						ElementTransformUtils.MoveElement(doc, ((Element)support2).Id, delta);
						doc.Regenerate();
						XYZ newOrigin = ((LocationPoint)((Element)support2).Location).Point;
						double remainingDist = newOrigin.DistanceTo(targetPoint) * 304.8;
						string moveDetail = $"Di chuyển: dX={delta.X * 304.8:F1}mm, dY={delta.Y * 304.8:F1}mm, dZ={delta.Z * 304.8:F1}mm. ";
						if (remainingDist > 0.5)
						{
							logs.Add(new SupportLogItem
							{
								ElementId = ((object)((Element)support2).Id).ToString(),
								TypeName = ((Element)support2.Symbol).Name,
								Status = "Hạn chế",
								StatusCode = "error",
								Detail = moveDetail + $"Cảnh báo: Đối tượng bị ràng buộc (constraint) hoặc hosted, thực tế còn lệch {remainingDist:F1}mm so với ống. {detailLog}"
							});
						}
						else
						{
							logs.Add(new SupportLogItem
							{
								ElementId = ((object)((Element)support2).Id).ToString(),
								TypeName = ((Element)support2.Symbol).Name,
								Status = "Thành công",
								StatusCode = "moved",
								Detail = moveDetail + "Căn chỉnh thành công theo " + methodUsed + ". " + detailLog
							});
						}
					}
					catch (Exception ex)
					{
						logs.Add(new SupportLogItem
						{
							ElementId = ((object)((Element)support2).Id).ToString(),
							TypeName = ((Element)support2.Symbol).Name,
							Status = "Lỗi",
							StatusCode = "error",
							Detail = "Lỗi khi thực hiện di chuyển: " + ex.Message + ". " + detailLog
						});
					}
				}
				tx.Commit();
			}
			finally
			{
				((IDisposable)tx)?.Dispose();
			}
		}
		catch (Exception ex2)
		{
			TaskDialog.Show("Align Support Error", ex2.Message);
		}
		OnCompleteLogs?.Invoke(logs);
	}

	private static double GetRodLength(FamilyInstance fi)
	{
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Invalid comparison between Unknown and I4
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Expected O, but got Unknown
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Invalid comparison between Unknown and I4
		string[] exactNames = new string[14]
		{
			"ROD LENGTH", "Rod Length", "rod length", "RodLength", "Chiều dài ty", "Chieu dai ty", "Chiều dài ty treo", "Chieu dai ty treo", "Độ dài ty", "Do dai ty",
			"PipeCenterToFloor", "ElementBottomToFloor", "SUPPORT LENGTH", "Support Length"
		};
		string[] array = exactNames;
		foreach (string name in array)
		{
			Parameter p = ((Element)fi).LookupParameter(name);
			if (p != null && (int)p.StorageType == 2)
			{
				return p.AsDouble();
			}
		}
		foreach (Parameter parameter in ((Element)fi).Parameters)
		{
			Parameter p2 = parameter;
			if ((int)p2.StorageType == 2)
			{
				string nameLower = p2.Definition.Name.ToLower();
				if ((nameLower.Contains("rod") && nameLower.Contains("length")) || (nameLower.Contains("ty") && (nameLower.Contains("dài") || nameLower.Contains("dai") || nameLower.Contains("dộ") || nameLower.Contains("do"))))
				{
					return p2.AsDouble();
				}
			}
		}
		return 0.0;
	}

	private static bool IsOriginAtTop(FamilyInstance fi, double rodLengthFeet)
	{
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		if (rodLengthFeet <= 0.01)
		{
			return false;
		}
		BoundingBoxXYZ bbox = ((Element)fi).get_BoundingBox(null);
		if (bbox == null)
		{
			return false;
		}
		double midZ = (bbox.Min.Z + bbox.Max.Z) / 2.0;
		XYZ origin = ((LocationPoint)((Element)fi).Location).Point;
		return origin.Z > midZ;
	}

	private static string GetElevationParametersDiagnostics(FamilyInstance fi)
	{
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Expected O, but got Unknown
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Invalid comparison between Unknown and I4
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Invalid comparison between Unknown and I4
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Invalid comparison between Unknown and I4
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Invalid comparison between Unknown and I4
		List<string> diag = new List<string>();
		string[] keywords = new string[9] { "offset", "elevation", "height", "level", "length", "rod", "độ cao", "cao độ", "khoảng cách" };
		foreach (Parameter parameter in ((Element)fi).Parameters)
		{
			Parameter p = parameter;
			string name = p.Definition.Name.ToLower();
			if (keywords.Any((string k) => name.Contains(k)))
			{
				string valStr = "";
				if ((int)p.StorageType == 2)
				{
					valStr = (p.AsDouble() * 304.8).ToString("F1") + "mm";
				}
				else if ((int)p.StorageType == 1)
				{
					valStr = p.AsInteger().ToString();
				}
				else if ((int)p.StorageType == 3)
				{
					valStr = p.AsString();
				}
				else if ((int)p.StorageType == 4)
				{
					valStr = ((object)p.AsElementId()).ToString();
				}
				diag.Add(p.Definition.Name + "=" + valStr);
			}
		}
		return string.Join(", ", diag);
	}

	public string GetName()
	{
		return "AlignSupportHandler";
	}

	private static XYZ FindIntersectionWithPipes(List<Pipe> pipes, XYZ origin, double halfSize, ref string detailLog)
	{
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Expected O, but got Unknown
		XYZ bestPoint = null;
		double bestDist3D = double.MaxValue;
		Pipe bestPipe = null;
		int outOfBoundsCount = 0;
		double nearestOutOfBoundsDist = double.MaxValue;
		UV uv = default(UV);
		double num = default(double);
		UV uvOrigin = default(UV);
		foreach (Pipe pipe in pipes)
		{
			Curve curve = ((LocationCurve)((Element)pipe).Location).Curve;
			Line pipeLine = (Line)(object)((curve is Line) ? curve : null);
			if ((GeometryObject)(object)pipeLine == (GeometryObject)null)
			{
				continue;
			}
			XYZ pipeDir = (((Curve)pipeLine).GetEndPoint(1) - ((Curve)pipeLine).GetEndPoint(0)).Normalize();
			XYZ normal = new XYZ(pipeDir.X, pipeDir.Y, 0.0);
			if (normal.IsAlmostEqualTo(XYZ.Zero))
			{
				continue;
			}
			normal = normal.Normalize();
			Plane plane;
			try
			{
				plane = Plane.CreateByNormalAndOrigin(normal, origin);
			}
			catch
			{
				continue;
			}
			XYZ intersection = IntersectLinePlane(pipeLine, plane);
			if (intersection == null)
			{
				continue;
			}
			((Surface)plane).Project(intersection, out uv, out num);
			((Surface)plane).Project(origin, out uvOrigin, out num);
			double du = Math.Abs(uv.U - uvOrigin.U);
			double dv = Math.Abs(uv.V - uvOrigin.V);
			double verticalLimit = 13.123359580052492;
			if (du <= halfSize && dv <= verticalLimit)
			{
				double dist3D = origin.DistanceTo(intersection);
				if (dist3D < bestDist3D)
				{
					bestDist3D = dist3D;
					bestPoint = intersection;
					bestPipe = pipe;
				}
			}
			else
			{
				outOfBoundsCount++;
				double currentDist = origin.DistanceTo(intersection);
				if (currentDist < nearestOutOfBoundsDist)
				{
					nearestOutOfBoundsDist = currentDist;
				}
			}
		}
		if (bestPoint != null)
		{
			detailLog = $"Khớp ống ID {((Element)bestPipe).Id} (Khoảng cách 3D lệch: {bestDist3D * 304.8:F1} mm)";
			return bestPoint;
		}
		if (outOfBoundsCount > 0)
		{
			detailLog = $"Có {outOfBoundsCount} ống cắt qua Plane của ống nhưng ngoài vùng 1000x1000mm (Khoảng cách gần nhất ngoài vùng: {nearestOutOfBoundsDist * 304.8:F1} mm)";
		}
		else
		{
			detailLog = "Không có ống nào cắt qua Plane";
		}
		return null;
	}

	private static XYZ IntersectLinePlane(Line line, Plane plane)
	{
		XYZ p0 = ((Curve)line).GetEndPoint(0);
		XYZ dir = (((Curve)line).GetEndPoint(1) - p0).Normalize();
		XYZ n = plane.Normal;
		XYZ o = plane.Origin;
		double denom = n.DotProduct(dir);
		if (Math.Abs(denom) < 1E-09)
		{
			return null;
		}
		double t = n.DotProduct(o - p0) / denom;
		return p0 + dir * t;
	}
}
