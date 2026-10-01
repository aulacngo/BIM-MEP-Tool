using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class ConnectSprinklerToPipe : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		UIApplication uiApp = commandData.Application;
		UIDocument uidoc = uiApp.ActiveUIDocument;
		if (uidoc == null || uidoc.Document == null)
		{
			message = "Khong co Document nao dang mo.";
			return Result.Failed;
		}
		Document doc = uidoc.Document;
		ConnectSprinklerToPipeWindow window = new ConnectSprinklerToPipeWindow();
		IntPtr h = uiApp.MainWindowHandle;
		if (h != IntPtr.Zero)
		{
			try { new System.Windows.Interop.WindowInteropHelper(window).Owner = h; } catch { }
		}
		window.ShowDialog();
		if (!window.IsOK)
		{
			return Result.Cancelled;
		}
		int selectedType = window.SelectedType;
		double zOffset = window.ZOffset / 304.8;
		double pipeDiameterFeet = window.PipeDiameter / 304.8;
		try
		{
			return (Result)(selectedType switch
			{
				1 => ExecutePendantType1(doc, uidoc, zOffset, pipeDiameterFeet), 
				2 => ExecutePendantType2(doc, uidoc, zOffset, pipeDiameterFeet), 
				3 => ExecutePendantType3(doc, uidoc, pipeDiameterFeet), 
				4 => ExecutePendantType4(doc, uidoc, zOffset, pipeDiameterFeet), 
				5 => ExecuteUprightType1(doc, uidoc, zOffset, pipeDiameterFeet), 
				6 => ExecuteUprightType2(doc, uidoc, pipeDiameterFeet), 
				7 => ExecuteUprightType3(doc, uidoc, zOffset, pipeDiameterFeet), 
				_ => Result.Cancelled, 
			});
		}
		catch (Autodesk.Revit.Exceptions.OperationCanceledException)
		{
			return Result.Cancelled;
		}
		catch (Exception ex)
		{
			message = ex.Message;
			return Result.Failed;
		}
	}

	private Result ExecutePendantType1(Document doc, UIDocument uidoc, double zOffset, double pipeDiameterFeet)
	{
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Expected O, but got Unknown
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Expected O, but got Unknown
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0266: Unknown result type (might be due to invalid IL or missing references)
		//IL_027d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0292: Unknown result type (might be due to invalid IL or missing references)
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0296: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_011d: Expected O, but got Unknown
		//IL_01de: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e5: Expected O, but got Unknown
		//IL_01e8: Unknown result type (might be due to invalid IL or missing references)
		IList<Reference> selectedInstances = uidoc.Selection.PickObjects((ObjectType)1, (ISelectionFilter)(object)new SelectSprinklers(), "Quet chon cac dau phun Sprinkler can ket noi (nhan Finish)");
		Reference pickPipe = uidoc.Selection.PickObject((ObjectType)1, (ISelectionFilter)(object)new SelectPipes(), "Chon ong nuoc chinh de ket noi vao");
		Element element = doc.GetElement(pickPipe);
		Pipe pipe = (Pipe)(object)((element is Pipe) ? element : null);
		TransactionGroup tg = new TransactionGroup(doc, "Connect Pendant Sprinkler Type 1");
		try
		{
			tg.Start();
			Transaction tMove = new Transaction(doc, "Move Sprinklers Below Pipe");
			try
			{
				try { tMove.GetFailureHandlingOptions().SetFailuresPreprocessor(new SuppressAllWarnings()); } catch { }
				tMove.Start();
				Location location = ((Element)pipe).Location;
				Curve curve = ((LocationCurve)((location is LocationCurve) ? location : null)).Curve;
				Line pipeLine = (Line)(object)((curve is Line) ? curve : null);
				double pipeZ = ((Curve)pipeLine).GetEndPoint(0).Z;
				double offsetBelow = 0.49212598425196846;
				foreach (Reference r in selectedInstances)
				{
					Element element2 = doc.GetElement(r);
					FamilyInstance sprinkler = (FamilyInstance)(object)((element2 is FamilyInstance) ? element2 : null);
					Connector connector = GetSprinklerConnector(sprinkler);
					if (connector != null)
					{
						double sprinklerZ = connector.Origin.Z;
						if (sprinklerZ >= pipeZ)
						{
							double targetZ = pipeZ - offsetBelow;
							double moveDistance = sprinklerZ - targetZ;
							ElementTransformUtils.MoveElement(doc, ((Element)sprinkler).Id, new XYZ(0.0, 0.0, 0.0 - moveDistance));
						}
					}
				}
				tMove.Commit();
			}
			finally
			{
				((IDisposable)tMove)?.Dispose();
			}
			List<XYZ> listPoint = new List<XYZ>();
			List<Pipe> listPipe = new List<Pipe>();
			foreach (Reference r2 in selectedInstances)
			{
				Element element3 = doc.GetElement(r2);
				FamilyInstance familyInstance = (FamilyInstance)(object)((element3 is FamilyInstance) ? element3 : null);
				(Pipe, XYZ) data = CreatePendantType1Pipes(doc, familyInstance, pipe, zOffset, pipeDiameterFeet);
				listPipe.Add(data.Item1);
				listPoint.Add(data.Item2);
			}
			List<Pipe> listNewPipes = SplitPipeAtPoints(doc, pipe, listPoint);
			Transaction t = new Transaction(doc, "Create tee");
			try
			{
				try { t.GetFailureHandlingOptions().SetFailuresPreprocessor(new SuppressAllWarnings()); } catch { }
				t.Start();
				for (int i = 0; i < listPipe.Count; i++)
				{
					try
					{
						Pipe verticalPipe2 = listPipe[i];
						XYZ point = listPoint[i];
						List<Pipe> list2Pipe = Find2PipesAtPoint(listNewPipes, point);
						if (list2Pipe.Count == 2)
						{
							CreateTeeFittingAtPoint(list2Pipe[0], list2Pipe[1], verticalPipe2, point);
						}
					}
					catch
					{
					}
				}
				t.Commit();
			}
			finally
			{
				((IDisposable)t)?.Dispose();
			}
			tg.Assimilate();
		}
		catch (Exception)
		{
			if (tg.HasStarted()) tg.RollBack();
			throw;
		}
		finally
		{
			((IDisposable)tg)?.Dispose();
		}
		return Result.Succeeded;
	}

	private (Pipe, XYZ) CreatePendantType1Pipes(Document doc, FamilyInstance sprinkler, Pipe mainPipe, double zOffset, double pipeDiameterFeet)
	{
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Expected O, but got Unknown
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Expected O, but got Unknown
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Expected O, but got Unknown
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c1: Unknown result type (might be due to invalid IL or missing references)
		Connector connector = GetSprinklerConnector(sprinkler);
		XYZ origin = connector.Origin;
		ElementId levelId = ((Element)sprinkler).get_Parameter((BuiltInParameter)(-1001352)).AsElementId();
		Element element = doc.GetElement(((Element)mainPipe).GetTypeId());
		PipeType pipeType = (PipeType)(object)((element is PipeType) ? element : null);
		Location location = ((Element)mainPipe).Location;
		Curve curve = ((LocationCurve)((location is LocationCurve) ? location : null)).Curve;
		Line pipeLine = (Line)(object)((curve is Line) ? curve : null);
		ElementId systemTypeId = ((Element)mainPipe).get_Parameter((BuiltInParameter)(-1140334)).AsElementId();
		Plane plane = Plane.CreateByNormalAndOrigin(pipeLine.Direction, origin);
		XYZ intersectionPoint = LineIntersectPlane(pipeLine, plane);
		XYZ topPoint1 = new XYZ(origin.X, origin.Y, intersectionPoint.Z + zOffset);
		XYZ topPoint2 = new XYZ(intersectionPoint.X, intersectionPoint.Y, intersectionPoint.Z + zOffset);
		Pipe verticalPipe2 = null;
		Transaction t = new Transaction(doc, "Create Pipe");
		try
		{
			try { t.GetFailureHandlingOptions().SetFailuresPreprocessor(new SuppressAllWarnings()); } catch { }
				t.Start();
			Pipe verticalPipe3 = Pipe.Create(doc, systemTypeId, ((Element)pipeType).Id, levelId, origin, topPoint1);
			((Element)verticalPipe3).get_Parameter((BuiltInParameter)(-1140225)).Set(pipeDiameterFeet);
			Connector cn0 = FindConnectorAtPoint(verticalPipe3, origin);
			if (cn0 != null && connector != null)
			{
				cn0.ConnectTo(connector);
			}
			Pipe horizontalPipe = Pipe.Create(doc, systemTypeId, ((Element)pipeType).Id, levelId, topPoint1, topPoint2);
			((Element)horizontalPipe).get_Parameter((BuiltInParameter)(-1140225)).Set(pipeDiameterFeet);
			verticalPipe2 = Pipe.Create(doc, systemTypeId, ((Element)pipeType).Id, levelId, topPoint2, intersectionPoint);
			((Element)verticalPipe2).get_Parameter((BuiltInParameter)(-1140225)).Set(pipeDiameterFeet);
			Connector cn1 = FindConnectorAtPoint(verticalPipe3, topPoint1);
			Connector cn2 = FindConnectorAtPoint(horizontalPipe, topPoint1);
			Connector cn3 = FindConnectorAtPoint(horizontalPipe, topPoint2);
			Connector cn4 = FindConnectorAtPoint(verticalPipe2, topPoint2);
			doc.Create.NewElbowFitting(cn1, cn2);
			doc.Create.NewElbowFitting(cn3, cn4);
			t.Commit();
		}
		finally
		{
			((IDisposable)t)?.Dispose();
		}
		return (verticalPipe2, intersectionPoint);
	}

	private Result ExecutePendantType2(Document doc, UIDocument uidoc, double zOffset, double pipeDiameterFeet)
	{
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Expected O, but got Unknown
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Expected O, but got Unknown
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0266: Unknown result type (might be due to invalid IL or missing references)
		//IL_027d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0292: Unknown result type (might be due to invalid IL or missing references)
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0296: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_011d: Expected O, but got Unknown
		//IL_01de: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e5: Expected O, but got Unknown
		//IL_01e8: Unknown result type (might be due to invalid IL or missing references)
		IList<Reference> selectedInstances = uidoc.Selection.PickObjects((ObjectType)1, (ISelectionFilter)(object)new SelectSprinklers(), "Quet chon cac dau phun Sprinkler can ket noi (nhan Finish)");
		Reference pickPipe = uidoc.Selection.PickObject((ObjectType)1, (ISelectionFilter)(object)new SelectPipes(), "Chon ong nuoc chinh de ket noi vao");
		Element element = doc.GetElement(pickPipe);
		Pipe pipe = (Pipe)(object)((element is Pipe) ? element : null);
		TransactionGroup tg = new TransactionGroup(doc, "Connect Pendant Sprinkler Type 2");
		try
		{
			tg.Start();
			Transaction tMove = new Transaction(doc, "Move Sprinklers Below Pipe");
			try
			{
				try { tMove.GetFailureHandlingOptions().SetFailuresPreprocessor(new SuppressAllWarnings()); } catch { }
				tMove.Start();
				Location location = ((Element)pipe).Location;
				Curve curve = ((LocationCurve)((location is LocationCurve) ? location : null)).Curve;
				Line pipeLine = (Line)(object)((curve is Line) ? curve : null);
				double pipeZ = ((Curve)pipeLine).GetEndPoint(0).Z;
				double offsetBelow = 0.49212598425196846;
				foreach (Reference r in selectedInstances)
				{
					Element element2 = doc.GetElement(r);
					FamilyInstance sprinkler = (FamilyInstance)(object)((element2 is FamilyInstance) ? element2 : null);
					Connector connector = GetSprinklerConnector(sprinkler);
					if (connector != null)
					{
						double sprinklerZ = connector.Origin.Z;
						if (sprinklerZ >= pipeZ)
						{
							double targetZ = pipeZ - offsetBelow;
							double moveDistance = sprinklerZ - targetZ;
							ElementTransformUtils.MoveElement(doc, ((Element)sprinkler).Id, new XYZ(0.0, 0.0, 0.0 - moveDistance));
						}
					}
				}
				tMove.Commit();
			}
			finally
			{
				((IDisposable)tMove)?.Dispose();
			}
			List<XYZ> listPoint = new List<XYZ>();
			List<Pipe> listPipe = new List<Pipe>();
			foreach (Reference r2 in selectedInstances)
			{
				Element element3 = doc.GetElement(r2);
				FamilyInstance familyInstance = (FamilyInstance)(object)((element3 is FamilyInstance) ? element3 : null);
				(Pipe, XYZ) data = CreatePendantType2Pipes(doc, familyInstance, pipe, zOffset, pipeDiameterFeet);
				listPipe.Add(data.Item1);
				listPoint.Add(data.Item2);
			}
			List<Pipe> listNewPipes = SplitPipeAtPoints(doc, pipe, listPoint);
			Transaction t = new Transaction(doc, "Create tee");
			try
			{
				try { t.GetFailureHandlingOptions().SetFailuresPreprocessor(new SuppressAllWarnings()); } catch { }
				t.Start();
				for (int i = 0; i < listPipe.Count; i++)
				{
					try
					{
						Pipe verticalPipe2 = listPipe[i];
						XYZ point = listPoint[i];
						List<Pipe> list2Pipe = Find2PipesAtPoint(listNewPipes, point);
						if (list2Pipe.Count == 2)
						{
							CreateTeeFittingAtPoint(list2Pipe[0], list2Pipe[1], verticalPipe2, point);
						}
					}
					catch
					{
					}
				}
				t.Commit();
			}
			finally
			{
				((IDisposable)t)?.Dispose();
			}
			tg.Assimilate();
		}
		catch (Exception)
		{
			if (tg.HasStarted()) tg.RollBack();
			throw;
		}
		finally
		{
			((IDisposable)tg)?.Dispose();
		}
		return Result.Succeeded;
	}

	private (Pipe, XYZ) CreatePendantType2Pipes(Document doc, FamilyInstance sprinkler, Pipe mainPipe, double zOffset, double pipeDiameterFeet)
	{
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Expected O, but got Unknown
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Expected O, but got Unknown
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Expected O, but got Unknown
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0226: Unknown result type (might be due to invalid IL or missing references)
		Connector connector = GetSprinklerConnector(sprinkler);
		XYZ origin = connector.Origin;
		ElementId levelId = ((Element)sprinkler).get_Parameter((BuiltInParameter)(-1001352)).AsElementId();
		Element element = doc.GetElement(((Element)mainPipe).GetTypeId());
		PipeType pipeType = (PipeType)(object)((element is PipeType) ? element : null);
		Location location = ((Element)mainPipe).Location;
		Curve curve = ((LocationCurve)((location is LocationCurve) ? location : null)).Curve;
		Line pipeLine = (Line)(object)((curve is Line) ? curve : null);
		ElementId systemTypeId = ((Element)mainPipe).get_Parameter((BuiltInParameter)(-1140334)).AsElementId();
		Plane plane = Plane.CreateByNormalAndOrigin(pipeLine.Direction, origin);
		XYZ intersectionPoint = LineIntersectPlane(pipeLine, plane);
		XYZ topPoint1 = new XYZ(origin.X, origin.Y, intersectionPoint.Z + zOffset);
		XYZ topPoint2 = new XYZ(intersectionPoint.X, intersectionPoint.Y, intersectionPoint.Z + zOffset);
		Pipe verticalPipe2 = null;
		Transaction t = new Transaction(doc, "Create Pipe");
		try
		{
			try { t.GetFailureHandlingOptions().SetFailuresPreprocessor(new SuppressAllWarnings()); } catch { }
				t.Start();
			double dn15 = 0.049212598425196846;
			Pipe verticalPipe3 = Pipe.Create(doc, systemTypeId, ((Element)pipeType).Id, levelId, origin, topPoint1);
			((Element)verticalPipe3).get_Parameter((BuiltInParameter)(-1140225)).Set(dn15);
			Connector cn0 = FindConnectorAtPoint(verticalPipe3, origin);
			if (cn0 != null && connector != null)
			{
				cn0.ConnectTo(connector);
			}
			Pipe horizontalPipe = Pipe.Create(doc, systemTypeId, ((Element)pipeType).Id, levelId, topPoint1, topPoint2);
			((Element)horizontalPipe).get_Parameter((BuiltInParameter)(-1140225)).Set(pipeDiameterFeet);
			verticalPipe2 = Pipe.Create(doc, systemTypeId, ((Element)pipeType).Id, levelId, topPoint2, intersectionPoint);
			((Element)verticalPipe2).get_Parameter((BuiltInParameter)(-1140225)).Set(pipeDiameterFeet);
			Connector cn1 = FindConnectorAtPoint(verticalPipe3, topPoint1);
			Connector cn2 = FindConnectorAtPoint(horizontalPipe, topPoint1);
			Connector cn3 = FindConnectorAtPoint(horizontalPipe, topPoint2);
			Connector cn4 = FindConnectorAtPoint(verticalPipe2, topPoint2);
			FamilyInstance elbowToSpirinkler = doc.Create.NewElbowFitting(cn1, cn2);
			doc.Create.NewElbowFitting(cn3, cn4);
			doc.Delete(((Element)verticalPipe3).Id);
			Connector unusedElbowConnector = GetUnusedConnector(elbowToSpirinkler);
			XYZ translation = unusedElbowConnector.Origin - connector.Origin;
			ElementTransformUtils.MoveElement(doc, ((Element)sprinkler).Id, translation);
			Connector newSprinklerConn = GetUnusedConnector(sprinkler);
			if (newSprinklerConn != null)
			{
				newSprinklerConn.ConnectTo(unusedElbowConnector);
			}
			t.Commit();
		}
		finally
		{
			((IDisposable)t)?.Dispose();
		}
		return (verticalPipe2, intersectionPoint);
	}

	private Result ExecutePendantType3(Document doc, UIDocument uidoc, double pipeDiameterFeet)
	{
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Expected O, but got Unknown
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Expected O, but got Unknown
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0264: Unknown result type (might be due to invalid IL or missing references)
		//IL_027b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0290: Unknown result type (might be due to invalid IL or missing references)
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0294: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_011d: Expected O, but got Unknown
		//IL_01dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e3: Expected O, but got Unknown
		//IL_01e6: Unknown result type (might be due to invalid IL or missing references)
		IList<Reference> selectedInstances = uidoc.Selection.PickObjects((ObjectType)1, (ISelectionFilter)(object)new SelectSprinklers(), "Quet chon cac dau phun Sprinkler can ket noi (nhan Finish)");
		Reference pickPipe = uidoc.Selection.PickObject((ObjectType)1, (ISelectionFilter)(object)new SelectPipes(), "Chon ong nuoc chinh de ket noi vao");
		Element element = doc.GetElement(pickPipe);
		Pipe pipe = (Pipe)(object)((element is Pipe) ? element : null);
		TransactionGroup tg = new TransactionGroup(doc, "Connect Pendant Sprinkler Type 3");
		try
		{
			tg.Start();
			Transaction tMove = new Transaction(doc, "Move Sprinklers Below Pipe");
			try
			{
				try { tMove.GetFailureHandlingOptions().SetFailuresPreprocessor(new SuppressAllWarnings()); } catch { }
				tMove.Start();
				Location location = ((Element)pipe).Location;
				Curve curve = ((LocationCurve)((location is LocationCurve) ? location : null)).Curve;
				Line pipeLine = (Line)(object)((curve is Line) ? curve : null);
				double pipeZ = ((Curve)pipeLine).GetEndPoint(0).Z;
				double offsetBelow = 0.49212598425196846;
				foreach (Reference r in selectedInstances)
				{
					Element element2 = doc.GetElement(r);
					FamilyInstance sprinkler = (FamilyInstance)(object)((element2 is FamilyInstance) ? element2 : null);
					Connector connector = GetSprinklerConnector(sprinkler);
					if (connector != null)
					{
						double sprinklerZ = connector.Origin.Z;
						if (sprinklerZ >= pipeZ)
						{
							double targetZ = pipeZ - offsetBelow;
							double moveDistance = sprinklerZ - targetZ;
							ElementTransformUtils.MoveElement(doc, ((Element)sprinkler).Id, new XYZ(0.0, 0.0, 0.0 - moveDistance));
						}
					}
				}
				tMove.Commit();
			}
			finally
			{
				((IDisposable)tMove)?.Dispose();
			}
			List<XYZ> listPoint = new List<XYZ>();
			List<Pipe> listPipe = new List<Pipe>();
			foreach (Reference r2 in selectedInstances)
			{
				Element element3 = doc.GetElement(r2);
				FamilyInstance familyInstance = (FamilyInstance)(object)((element3 is FamilyInstance) ? element3 : null);
				(Pipe, XYZ) data = CreatePendantType3Pipes(doc, familyInstance, pipe, pipeDiameterFeet);
				listPipe.Add(data.Item1);
				listPoint.Add(data.Item2);
			}
			List<Pipe> listNewPipes = SplitPipeAtPoints(doc, pipe, listPoint);
			Transaction t = new Transaction(doc, "Create tee");
			try
			{
				try { t.GetFailureHandlingOptions().SetFailuresPreprocessor(new SuppressAllWarnings()); } catch { }
				t.Start();
				for (int i = 0; i < listPipe.Count; i++)
				{
					try
					{
						Pipe horizontalPipe = listPipe[i];
						XYZ point = listPoint[i];
						List<Pipe> list2Pipe = Find2PipesAtPoint(listNewPipes, point);
						if (list2Pipe.Count == 2)
						{
							CreateTeeFittingAtPoint(list2Pipe[0], list2Pipe[1], horizontalPipe, point);
						}
					}
					catch
					{
					}
				}
				t.Commit();
			}
			finally
			{
				((IDisposable)t)?.Dispose();
			}
			tg.Assimilate();
		}
		catch (Exception)
		{
			if (tg.HasStarted()) tg.RollBack();
			throw;
		}
		finally
		{
			((IDisposable)tg)?.Dispose();
		}
		return Result.Succeeded;
	}

	private (Pipe, XYZ) CreatePendantType3Pipes(Document doc, FamilyInstance sprinkler, Pipe mainPipe, double pipeDiameterFeet)
	{
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Expected O, but got Unknown
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Expected O, but got Unknown
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b3: Unknown result type (might be due to invalid IL or missing references)
		Connector connector = GetSprinklerConnector(sprinkler);
		XYZ origin = connector.Origin;
		ElementId levelId = ((Element)sprinkler).get_Parameter((BuiltInParameter)(-1001352)).AsElementId();
		Element element = doc.GetElement(((Element)mainPipe).GetTypeId());
		PipeType pipeType = (PipeType)(object)((element is PipeType) ? element : null);
		Location location = ((Element)mainPipe).Location;
		Curve curve = ((LocationCurve)((location is LocationCurve) ? location : null)).Curve;
		Line pipeLine = (Line)(object)((curve is Line) ? curve : null);
		ElementId systemTypeId = ((Element)mainPipe).get_Parameter((BuiltInParameter)(-1140334)).AsElementId();
		Plane plane = Plane.CreateByNormalAndOrigin(pipeLine.Direction, origin);
		XYZ intersectionPoint = LineIntersectPlane(pipeLine, plane);
		XYZ topPoint = new XYZ(origin.X, origin.Y, intersectionPoint.Z);
		Pipe horizontalPipe = null;
		Transaction t = new Transaction(doc, "Create Pipe");
		try
		{
			try { t.GetFailureHandlingOptions().SetFailuresPreprocessor(new SuppressAllWarnings()); } catch { }
				t.Start();
			double dn15 = 0.049212598425196846;
			Pipe verticalPipe = Pipe.Create(doc, systemTypeId, ((Element)pipeType).Id, levelId, origin, topPoint);
			((Element)verticalPipe).get_Parameter((BuiltInParameter)(-1140225)).Set(dn15);
			Connector cn0 = FindConnectorAtPoint(verticalPipe, origin);
			if (cn0 != null && connector != null)
			{
				cn0.ConnectTo(connector);
			}
			horizontalPipe = Pipe.Create(doc, systemTypeId, ((Element)pipeType).Id, levelId, intersectionPoint, topPoint);
			((Element)horizontalPipe).get_Parameter((BuiltInParameter)(-1140225)).Set(pipeDiameterFeet);
			Connector cn1 = FindConnectorAtPoint(verticalPipe, topPoint);
			Connector cn2 = FindConnectorAtPoint(horizontalPipe, topPoint);
			FamilyInstance elbowToSpirinkler = doc.Create.NewElbowFitting(cn1, cn2);
			doc.Delete(((Element)verticalPipe).Id);
			Connector unusedElbowConnector = GetUnusedConnector(elbowToSpirinkler);
			XYZ translation = unusedElbowConnector.Origin - connector.Origin;
			ElementTransformUtils.MoveElement(doc, ((Element)sprinkler).Id, translation);
			Connector newSprinklerConn = GetUnusedConnector(sprinkler);
			if (newSprinklerConn != null)
			{
				newSprinklerConn.ConnectTo(unusedElbowConnector);
			}
			t.Commit();
		}
		finally
		{
			((IDisposable)t)?.Dispose();
		}
		return (horizontalPipe, intersectionPoint);
	}

	private Result ExecutePendantType4(Document doc, UIDocument uidoc, double zOffset, double pipeDiameterFeet)
	{
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Expected O, but got Unknown
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Expected O, but got Unknown
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		//IL_014e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0158: Expected O, but got Unknown
		//IL_0176: Unknown result type (might be due to invalid IL or missing references)
		//IL_0180: Expected O, but got Unknown
		//IL_05c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_05e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_05e8: Expected O, but got Unknown
		//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f9: Expected O, but got Unknown
		//IL_05eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_064c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0653: Expected O, but got Unknown
		//IL_0663: Unknown result type (might be due to invalid IL or missing references)
		//IL_066a: Expected O, but got Unknown
		//IL_06c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_06d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_06f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_06f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_052e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0535: Expected O, but got Unknown
		//IL_0538: Unknown result type (might be due to invalid IL or missing references)
		double dn15 = 0.049212598425196846;
		IList<Reference> selection = uidoc.Selection.PickObjects((ObjectType)1, (ISelectionFilter)(object)new SelectSprinklers(), "Quet chon cac dau phun Sprinkler can ket noi (nhan Finish)");
		List<FamilyInstance> sprinklers = selection.Select((Reference x) => doc.GetElement(x)).Cast<FamilyInstance>().ToList();
		Reference pickPipe = uidoc.Selection.PickObject((ObjectType)1, (ISelectionFilter)(object)new FilterPipe());
		Element element = doc.GetElement(pickPipe);
		Pipe mainPipe = (Pipe)(object)((element is Pipe) ? element : null);
		Element element2 = doc.GetElement(((Element)mainPipe).GetTypeId());
		PipeType pipeType = (PipeType)(object)((element2 is PipeType) ? element2 : null);
		Location location = ((Element)mainPipe).Location;
		Curve curve = ((LocationCurve)((location is LocationCurve) ? location : null)).Curve;
		Line mainLine = (Line)(object)((curve is Line) ? curve : null);
		ElementId levelId = ((Element)sprinklers[0]).get_Parameter((BuiltInParameter)(-1001352)).AsElementId();
		ElementId systemTypeId = ((Element)mainPipe).get_Parameter((BuiltInParameter)(-1140334)).AsElementId();
		TransactionGroup tg = new TransactionGroup(doc, "Connect Pendant Sprinkler Type 4");
		try
		{
			tg.Start();
			Pipe horizontalPipe = null;
			List<(Pipe, XYZ)> listPipes1 = new List<(Pipe, XYZ)>();
			List<(Pipe, XYZ)> listPipes2 = new List<(Pipe, XYZ)>();
			Transaction t = new Transaction(doc, "Create Pipe");
			try
			{
				try { t.GetFailureHandlingOptions().SetFailuresPreprocessor(new SuppressAllWarnings()); } catch { }
				t.Start();
				(FamilyInstance, FamilyInstance) list2Sprinklers = GetStartAndEndSprinkler(sprinklers);
				FamilyInstance firstSp = list2Sprinklers.Item1;
				FamilyInstance lastSp = list2Sprinklers.Item2;
				XYZ offsetPoint1 = GetOrigin(firstSp) - new XYZ(0.0, 0.0, 0.0 - zOffset);
				XYZ offsetPoint2 = GetOrigin(lastSp) - new XYZ(0.0, 0.0, 0.0 - zOffset);
				horizontalPipe = Pipe.Create(doc, systemTypeId, ((Element)pipeType).Id, levelId, offsetPoint1, offsetPoint2);
				((Element)horizontalPipe).get_Parameter((BuiltInParameter)(-1140225)).Set(pipeDiameterFeet);
				foreach (FamilyInstance sprinkler in sprinklers)
				{
					Connector connector = GetSprinklerConnector(sprinkler);
					XYZ bottomPoint = connector.Origin - new XYZ(0.0, 0.0, 0.0 - zOffset);
					Pipe verticalPipe = Pipe.Create(doc, ((Element)pipeType).Id, levelId, connector, bottomPoint);
					((Element)verticalPipe).get_Parameter((BuiltInParameter)(-1140334)).Set(systemTypeId);
					if (((Element)sprinkler).Id == ((Element)firstSp).Id || ((Element)sprinkler).Id == ((Element)lastSp).Id)
					{
						((Element)verticalPipe).get_Parameter((BuiltInParameter)(-1140225)).Set(dn15);
						listPipes1.Add((verticalPipe, bottomPoint));
					}
					else
					{
						((Element)verticalPipe).get_Parameter((BuiltInParameter)(-1140225)).Set(pipeDiameterFeet);
						listPipes2.Add((verticalPipe, bottomPoint));
					}
				}
				Pipe verPipe1 = listPipes1[0].Item1;
				XYZ point1 = listPipes1[0].Item2;
				Pipe verPipe2 = listPipes1[1].Item1;
				XYZ point2 = listPipes1[1].Item2;
				Connector cn1 = FindConnectorAtPoint(horizontalPipe, point1);
				Connector cn2 = FindConnectorAtPoint(horizontalPipe, point2);
				Connector cn3 = FindConnectorAtPoint(verPipe1, point1);
				Connector cn4 = FindConnectorAtPoint(verPipe2, point2);
				FamilyInstance elbow1 = doc.Create.NewElbowFitting(cn1, cn3);
				FamilyInstance elbow2 = doc.Create.NewElbowFitting(cn2, cn4);
				doc.Delete(((Element)verPipe1).Id);
				doc.Delete(((Element)verPipe2).Id);
				Connector elbowConnector1 = ((IEnumerable)elbow1.MEPModel.ConnectorManager.Connectors).Cast<Connector>().FirstOrDefault((Connector c) => !c.IsConnected);
				Connector elbowConnector2 = ((IEnumerable)elbow2.MEPModel.ConnectorManager.Connectors).Cast<Connector>().FirstOrDefault((Connector c) => !c.IsConnected);
				Location location2 = ((Element)firstSp).Location;
				Location obj = ((location2 is LocationPoint) ? location2 : null);
				XYZ firstSpPoint = ((obj != null) ? ((LocationPoint)obj).Point : null) ?? XYZ.Zero;
				Location location3 = ((Element)lastSp).Location;
				Location obj2 = ((location3 is LocationPoint) ? location3 : null);
				XYZ lastSpPoint = ((obj2 != null) ? ((LocationPoint)obj2).Point : null) ?? XYZ.Zero;
				double d1_1 = elbowConnector1.Origin.DistanceTo(firstSpPoint);
				double d1_2 = elbowConnector1.Origin.DistanceTo(lastSpPoint);
				double d2_1 = elbowConnector2.Origin.DistanceTo(firstSpPoint);
				double d2_2 = elbowConnector2.Origin.DistanceTo(lastSpPoint);
				FamilyInstance sprinklerForElbow1 = ((d1_1 + d2_2 <= d1_2 + d2_1) ? firstSp : lastSp);
				FamilyInstance sprinklerForElbow2 = ((d1_1 + d2_2 <= d1_2 + d2_1) ? lastSp : firstSp);
				ConnectElementToUnusedElbow_Silent(doc, elbow1, sprinklerForElbow1);
				ConnectElementToUnusedElbow_Silent(doc, elbow2, sprinklerForElbow2);
				t.Commit();
			}
			finally
			{
				((IDisposable)t)?.Dispose();
			}
			List<XYZ> splitPoints = listPipes2.Select(((Pipe, XYZ) x) => x.Item2).ToList();
			List<Pipe> listNewPipes = SplitPipeAtPoints(doc, horizontalPipe, splitPoints);
			Transaction t2 = new Transaction(doc, "Create tee");
			try
			{
				try { t2.GetFailureHandlingOptions().SetFailuresPreprocessor(new SuppressAllWarnings()); } catch { }
				t2.Start();
				for (int i = 0; i < listPipes2.Count; i++)
				{
					try
					{
						Pipe verticalPipe2 = listPipes2[i].Item1;
						XYZ point3 = listPipes2[i].Item2;
						List<Pipe> list2Pipe = Find2PipesAtPoint(listNewPipes, point3);
						if (list2Pipe.Count == 2)
						{
							CreateTeeFittingAtPoint(list2Pipe[0], list2Pipe[1], verticalPipe2, point3);
						}
					}
					catch
					{
					}
				}
				t2.Commit();
			}
			finally
			{
				((IDisposable)t2)?.Dispose();
			}
			Transaction t3 = new Transaction(doc, "SplitPipeAndCreateTee");
			try
			{
				try { t3.GetFailureHandlingOptions().SetFailuresPreprocessor(new SuppressAllWarnings()); } catch { }
				t3.Start();
				(XYZ, Pipe) intersection = GetIntersectionPipe(mainPipe, listNewPipes);
				XYZ intersecPoint = intersection.Item1;
				Pipe intersecPipe = intersection.Item2;
				double botZ = ((Curve)mainLine).GetEndPoint(0).Z;
				Location location4 = ((Element)intersecPipe).Location;
				double topZ = ((LocationCurve)((location4 is LocationCurve) ? location4 : null)).Curve.GetEndPoint(0).Z;
				XYZ botPoint = new XYZ(intersecPoint.X, intersecPoint.Y, botZ);
				XYZ topPoint = new XYZ(intersecPoint.X, intersecPoint.Y, topZ);
				Pipe verPipe3 = Pipe.Create(doc, systemTypeId, ((Element)pipeType).Id, levelId, botPoint, topPoint);
				((Element)verPipe3).get_Parameter((BuiltInParameter)(-1140225)).Set(pipeDiameterFeet);
				SplitPipeAndCreateTee(doc, mainPipe, verPipe3, botPoint);
				SplitPipeAndCreateTee(doc, intersecPipe, verPipe3, topPoint);
				t3.Commit();
			}
			finally
			{
				((IDisposable)t3)?.Dispose();
			}
			tg.Assimilate();
		}
		catch (Exception)
		{
			if (tg.HasStarted()) tg.RollBack();
			throw;
		}
		finally
		{
			((IDisposable)tg)?.Dispose();
		}
		return Result.Succeeded;
	}

	private Result ExecuteUprightType1(Document doc, UIDocument uidoc, double zOffset, double pipeDiameterFeet)
	{
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Expected O, but got Unknown
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0101: Expected O, but got Unknown
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_0140: Unknown result type (might be due to invalid IL or missing references)
		//IL_014a: Expected O, but got Unknown
		//IL_0167: Unknown result type (might be due to invalid IL or missing references)
		//IL_0171: Expected O, but got Unknown
		//IL_0445: Unknown result type (might be due to invalid IL or missing references)
		//IL_0466: Unknown result type (might be due to invalid IL or missing references)
		//IL_046d: Expected O, but got Unknown
		//IL_01de: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e8: Expected O, but got Unknown
		//IL_0470: Unknown result type (might be due to invalid IL or missing references)
		//IL_04d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_04d8: Expected O, but got Unknown
		//IL_04e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ef: Expected O, but got Unknown
		//IL_0546: Unknown result type (might be due to invalid IL or missing references)
		//IL_0353: Unknown result type (might be due to invalid IL or missing references)
		//IL_055e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0575: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ba: Expected O, but got Unknown
		//IL_0579: Unknown result type (might be due to invalid IL or missing references)
		//IL_03bd: Unknown result type (might be due to invalid IL or missing references)
		IList<Reference> selection = uidoc.Selection.PickObjects((ObjectType)1, (ISelectionFilter)(object)new SelectSprinklers(), "Quet chon cac dau phun Sprinkler can ket noi (nhan Finish)");
		List<FamilyInstance> sprinklers = selection.Select((Reference x) => doc.GetElement(x)).Cast<FamilyInstance>().ToList();
		Reference pickPipe = uidoc.Selection.PickObject((ObjectType)1, (ISelectionFilter)(object)new FilterPipe());
		Element element = doc.GetElement(pickPipe);
		Pipe mainPipe = (Pipe)(object)((element is Pipe) ? element : null);
		Element element2 = doc.GetElement(((Element)mainPipe).GetTypeId());
		PipeType pipeType = (PipeType)(object)((element2 is PipeType) ? element2 : null);
		Location location = ((Element)mainPipe).Location;
		Curve curve = ((LocationCurve)((location is LocationCurve) ? location : null)).Curve;
		Line mainLine = (Line)(object)((curve is Line) ? curve : null);
		ElementId levelId = ((Element)sprinklers[0]).get_Parameter((BuiltInParameter)(-1001352)).AsElementId();
		ElementId systemTypeId = ((Element)mainPipe).get_Parameter((BuiltInParameter)(-1140334)).AsElementId();
		TransactionGroup tg = new TransactionGroup(doc, "Connect Upright Sprinkler Type 1");
		try
		{
			tg.Start();
			Pipe horizontalPipe = null;
			List<(Pipe, XYZ)> listPipes1 = new List<(Pipe, XYZ)>();
			List<(Pipe, XYZ)> listPipes2 = new List<(Pipe, XYZ)>();
			Transaction t = new Transaction(doc, "Create Pipe");
			try
			{
				try { t.GetFailureHandlingOptions().SetFailuresPreprocessor(new SuppressAllWarnings()); } catch { }
				t.Start();
				(FamilyInstance, FamilyInstance) list2Sprinklers = GetStartAndEndSprinkler(sprinklers);
				FamilyInstance firstSp = list2Sprinklers.Item1;
				FamilyInstance lastSp = list2Sprinklers.Item2;
				XYZ offsetPoint1 = GetOrigin(firstSp) - new XYZ(0.0, 0.0, zOffset);
				XYZ offsetPoint2 = GetOrigin(lastSp) - new XYZ(0.0, 0.0, zOffset);
				horizontalPipe = Pipe.Create(doc, systemTypeId, ((Element)pipeType).Id, levelId, offsetPoint1, offsetPoint2);
				((Element)horizontalPipe).get_Parameter((BuiltInParameter)(-1140225)).Set(pipeDiameterFeet);
				foreach (FamilyInstance sprinkler in sprinklers)
				{
					Connector connector = GetSprinklerConnector(sprinkler);
					XYZ bottomPoint = connector.Origin - new XYZ(0.0, 0.0, zOffset);
					Pipe verticalPipe = Pipe.Create(doc, systemTypeId, ((Element)pipeType).Id, levelId, connector.Origin, bottomPoint);
					((Element)verticalPipe).get_Parameter((BuiltInParameter)(-1140225)).Set(pipeDiameterFeet);
					Connector cn0 = FindConnectorAtPoint(verticalPipe, connector.Origin);
					if (cn0 != null)
					{
						cn0.ConnectTo(connector);
					}
					if (((Element)sprinkler).Id == ((Element)firstSp).Id || ((Element)sprinkler).Id == ((Element)lastSp).Id)
					{
						listPipes1.Add((verticalPipe, bottomPoint));
					}
					else
					{
						listPipes2.Add((verticalPipe, bottomPoint));
					}
				}
				Pipe verPipe1 = listPipes1[0].Item1;
				XYZ point1 = listPipes1[0].Item2;
				Pipe verPipe2 = listPipes1[1].Item1;
				XYZ point2 = listPipes1[1].Item2;
				Connector cn1 = FindConnectorAtPoint(horizontalPipe, point1);
				Connector cn2 = FindConnectorAtPoint(horizontalPipe, point2);
				Connector cn3 = FindConnectorAtPoint(verPipe1, point1);
				Connector cn4 = FindConnectorAtPoint(verPipe2, point2);
				doc.Create.NewElbowFitting(cn1, cn3);
				doc.Create.NewElbowFitting(cn2, cn4);
				t.Commit();
			}
			finally
			{
				((IDisposable)t)?.Dispose();
			}
			List<XYZ> splitPoints = listPipes2.Select(((Pipe, XYZ) x) => x.Item2).ToList();
			List<Pipe> listNewPipes = SplitPipeAtPoints(doc, horizontalPipe, splitPoints);
			Transaction t2 = new Transaction(doc, "Create tee");
			try
			{
				try { t2.GetFailureHandlingOptions().SetFailuresPreprocessor(new SuppressAllWarnings()); } catch { }
				t2.Start();
				for (int i = 0; i < listPipes2.Count; i++)
				{
					try
					{
						Pipe verticalPipe2 = listPipes2[i].Item1;
						XYZ point3 = listPipes2[i].Item2;
						List<Pipe> list2Pipe = Find2PipesAtPoint(listNewPipes, point3);
						if (list2Pipe.Count == 2)
						{
							CreateTeeFittingAtPoint(list2Pipe[0], list2Pipe[1], verticalPipe2, point3);
						}
					}
					catch
					{
					}
				}
				t2.Commit();
			}
			finally
			{
				((IDisposable)t2)?.Dispose();
			}
			Transaction t3 = new Transaction(doc, "SplitPipeAndCreateTee");
			try
			{
				try { t3.GetFailureHandlingOptions().SetFailuresPreprocessor(new SuppressAllWarnings()); } catch { }
				t3.Start();
				(XYZ, Pipe) intersection = GetIntersectionPipe(mainPipe, listNewPipes);
				XYZ intersecPoint = intersection.Item1;
				Pipe intersecPipe = intersection.Item2;
				double botZ = ((Curve)mainLine).GetEndPoint(0).Z;
				Location location2 = ((Element)intersecPipe).Location;
				double topZ = ((LocationCurve)((location2 is LocationCurve) ? location2 : null)).Curve.GetEndPoint(0).Z;
				XYZ botPoint = new XYZ(intersecPoint.X, intersecPoint.Y, botZ);
				XYZ topPoint = new XYZ(intersecPoint.X, intersecPoint.Y, topZ);
				Pipe verPipe3 = Pipe.Create(doc, systemTypeId, ((Element)pipeType).Id, levelId, botPoint, topPoint);
				((Element)verPipe3).get_Parameter((BuiltInParameter)(-1140225)).Set(pipeDiameterFeet);
				SplitPipeAndCreateTee(doc, mainPipe, verPipe3, botPoint);
				SplitPipeAndCreateTee(doc, intersecPipe, verPipe3, topPoint);
				t3.Commit();
			}
			finally
			{
				((IDisposable)t3)?.Dispose();
			}
			tg.Assimilate();
		}
		catch (Exception)
		{
			if (tg.HasStarted()) tg.RollBack();
			throw;
		}
		finally
		{
			((IDisposable)tg)?.Dispose();
		}
		return Result.Succeeded;
	}

	private Result ExecuteUprightType2(Document doc, UIDocument uidoc, double pipeDiameterFeet)
	{
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Expected O, but got Unknown
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Expected O, but got Unknown
		//IL_018f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0196: Expected O, but got Unknown
		//IL_04ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_0199: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b4: Expected O, but got Unknown
		//IL_04c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_04da: Unknown result type (might be due to invalid IL or missing references)
		//IL_021d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0224: Expected O, but got Unknown
		//IL_04de: Unknown result type (might be due to invalid IL or missing references)
		//IL_0227: Unknown result type (might be due to invalid IL or missing references)
		//IL_0269: Unknown result type (might be due to invalid IL or missing references)
		//IL_0270: Expected O, but got Unknown
		//IL_037e: Unknown result type (might be due to invalid IL or missing references)
		//IL_03de: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e5: Expected O, but got Unknown
		//IL_03e8: Unknown result type (might be due to invalid IL or missing references)
		IList<Reference> selection = uidoc.Selection.PickObjects((ObjectType)1, (ISelectionFilter)(object)new SelectSprinklers(), "Quet chon cac dau phun Sprinkler can ket noi (nhan Finish)");
		List<FamilyInstance> sprinklers = selection.Select((Reference x) => doc.GetElement(x)).Cast<FamilyInstance>().ToList();
		Reference pickPipe = uidoc.Selection.PickObject((ObjectType)1, (ISelectionFilter)(object)new FilterPipe());
		Element element = doc.GetElement(pickPipe);
		Pipe mainPipe = (Pipe)(object)((element is Pipe) ? element : null);
		Element element2 = doc.GetElement(((Element)mainPipe).GetTypeId());
		PipeType pipeType = (PipeType)(object)((element2 is PipeType) ? element2 : null);
		Location location = ((Element)mainPipe).Location;
		Curve curve = ((LocationCurve)((location is LocationCurve) ? location : null)).Curve;
		Line mainLine = (Line)(object)((curve is Line) ? curve : null);
		ElementId levelId = ((Element)sprinklers[0]).get_Parameter((BuiltInParameter)(-1001352)).AsElementId();
		ElementId systemTypeId = ((Element)mainPipe).get_Parameter((BuiltInParameter)(-1140334)).AsElementId();
		(FamilyInstance, FamilyInstance) list2Sprinklers = GetStartAndEndSprinkler(sprinklers);
		FamilyInstance firstSp = list2Sprinklers.Item1;
		FamilyInstance lastSp = list2Sprinklers.Item2;
		XYZ sp = ((Curve)mainLine).GetEndPoint(0);
		double z = sp.Z;
		XYZ offsetPoint1 = new XYZ(GetOrigin(firstSp).X, GetOrigin(firstSp).Y, z);
		XYZ offsetPoint2 = new XYZ(GetOrigin(lastSp).X, GetOrigin(lastSp).Y, z);
		Plane plane = Plane.CreateByNormalAndOrigin(mainLine.Direction, offsetPoint1);
		XYZ intersectionPoint = LineIntersectPlane(mainLine, plane);
		double distance1 = intersectionPoint.DistanceTo(offsetPoint1);
		double distance2 = intersectionPoint.DistanceTo(offsetPoint2);
		FamilyInstance endSprinkler = ((distance1 < distance2) ? lastSp : firstSp);
		XYZ endPoint = ((distance1 < distance2) ? offsetPoint2 : offsetPoint1);
		TransactionGroup tg = new TransactionGroup(doc, "Connect Upright Sprinkler Type 2");
		try
		{
			tg.Start();
			Pipe horizontalPipe = null;
			Transaction t = new Transaction(doc, "create pipe1");
			try
			{
				try { t.GetFailureHandlingOptions().SetFailuresPreprocessor(new SuppressAllWarnings()); } catch { }
				t.Start();
				horizontalPipe = Pipe.Create(doc, systemTypeId, ((Element)pipeType).Id, levelId, intersectionPoint, endPoint);
				((Element)horizontalPipe).get_Parameter((BuiltInParameter)(-1140225)).Set(pipeDiameterFeet);
				t.Commit();
			}
			finally
			{
				((IDisposable)t)?.Dispose();
			}
			List<(Pipe, XYZ)> listPipes1 = new List<(Pipe, XYZ)>();
			List<(Pipe, XYZ)> listPipes2 = new List<(Pipe, XYZ)>();
			Transaction t2 = new Transaction(doc, "create pipe2");
			try
			{
				try { t2.GetFailureHandlingOptions().SetFailuresPreprocessor(new SuppressAllWarnings()); } catch { }
				t2.Start();
				foreach (FamilyInstance sprinkler in sprinklers)
				{
					Connector connector = GetSprinklerConnector(sprinkler);
					XYZ bottomPoint = new XYZ(connector.Origin.X, connector.Origin.Y, z);
					Pipe verticalPipe = Pipe.Create(doc, ((Element)pipeType).Id, levelId, connector, bottomPoint);
					((Element)verticalPipe).get_Parameter((BuiltInParameter)(-1140225)).Set(pipeDiameterFeet);
					((Element)verticalPipe).get_Parameter((BuiltInParameter)(-1140334)).Set(systemTypeId);
					if (((Element)sprinkler).Id == ((Element)endSprinkler).Id)
					{
						listPipes1.Add((verticalPipe, bottomPoint));
					}
					else
					{
						listPipes2.Add((verticalPipe, bottomPoint));
					}
				}
				if (listPipes1.Any())
				{
					Pipe verPipe1 = listPipes1[0].Item1;
					XYZ point1 = listPipes1[0].Item2;
					Connector cn1 = FindConnectorAtPoint(horizontalPipe, point1);
					Connector cn2 = FindConnectorAtPoint(verPipe1, point1);
					if (cn1 != null && cn2 != null)
					{
						doc.Create.NewElbowFitting(cn1, cn2);
					}
				}
				t2.Commit();
			}
			finally
			{
				((IDisposable)t2)?.Dispose();
			}
			List<XYZ> splitPoints = listPipes2.Select(((Pipe, XYZ) x) => x.Item2).ToList();
			List<Pipe> listNewPipes = SplitPipeAtPoints(doc, horizontalPipe, splitPoints);
			Transaction t3 = new Transaction(doc, "Create tee");
			try
			{
				try { t3.GetFailureHandlingOptions().SetFailuresPreprocessor(new SuppressAllWarnings()); } catch { }
				t3.Start();
				for (int i = 0; i < listPipes2.Count; i++)
				{
					try
					{
						Pipe verticalPipe2 = listPipes2[i].Item1;
						XYZ point2 = listPipes2[i].Item2;
						List<Pipe> list2Pipe = Find2PipesAtPoint(listNewPipes, point2);
						if (list2Pipe.Count == 2)
						{
							CreateTeeFittingAtPoint(list2Pipe[0], list2Pipe[1], verticalPipe2, point2);
						}
					}
					catch
					{
					}
				}
				Pipe horiPipe = listNewPipes.FirstOrDefault((Pipe pipe) => ((IEnumerable)((MEPCurve)pipe).ConnectorManager.Connectors).Cast<Connector>().Count((Connector c) => !c.IsConnected) == 1);
				SplitPipeAndCreateTee(doc, mainPipe, horiPipe, intersectionPoint);
				t3.Commit();
			}
			finally
			{
				((IDisposable)t3)?.Dispose();
			}
			tg.Assimilate();
		}
		catch (Exception)
		{
			if (tg.HasStarted()) tg.RollBack();
			throw;
		}
		finally
		{
			((IDisposable)tg)?.Dispose();
		}
		return Result.Succeeded;
	}

	private Result ExecuteUprightType3(Document doc, UIDocument uidoc, double zOffset, double pipeDiameterFeet)
	{
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Expected O, but got Unknown
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_012a: Expected O, but got Unknown
		//IL_015d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0164: Expected O, but got Unknown
		//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01aa: Expected O, but got Unknown
		//IL_01ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c8: Expected O, but got Unknown
		//IL_01cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0202: Unknown result type (might be due to invalid IL or missing references)
		//IL_0231: Unknown result type (might be due to invalid IL or missing references)
		//IL_0238: Expected O, but got Unknown
		//IL_0520: Unknown result type (might be due to invalid IL or missing references)
		//IL_023b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0538: Unknown result type (might be due to invalid IL or missing references)
		//IL_054f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0553: Unknown result type (might be due to invalid IL or missing references)
		//IL_027d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0287: Expected O, but got Unknown
		//IL_039e: Unknown result type (might be due to invalid IL or missing references)
		//IL_03fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0405: Expected O, but got Unknown
		//IL_0408: Unknown result type (might be due to invalid IL or missing references)
		IList<Reference> selection = uidoc.Selection.PickObjects((ObjectType)1, (ISelectionFilter)(object)new SelectSprinklers(), "Quet chon cac dau phun Sprinkler can ket noi (nhan Finish)");
		List<FamilyInstance> sprinklers = selection.Select((Reference x) => doc.GetElement(x)).Cast<FamilyInstance>().ToList();
		Reference pickPipe = uidoc.Selection.PickObject((ObjectType)1, (ISelectionFilter)(object)new FilterPipe());
		Element element = doc.GetElement(pickPipe);
		Pipe mainPipe = (Pipe)(object)((element is Pipe) ? element : null);
		Element element2 = doc.GetElement(((Element)mainPipe).GetTypeId());
		PipeType pipeType = (PipeType)(object)((element2 is PipeType) ? element2 : null);
		Location location = ((Element)mainPipe).Location;
		Curve curve = ((LocationCurve)((location is LocationCurve) ? location : null)).Curve;
		Line mainLine = (Line)(object)((curve is Line) ? curve : null);
		ElementId levelId = ((Element)sprinklers[0]).get_Parameter((BuiltInParameter)(-1001352)).AsElementId();
		ElementId systemTypeId = ((Element)mainPipe).get_Parameter((BuiltInParameter)(-1140334)).AsElementId();
		(FamilyInstance, FamilyInstance) list2Sprinklers = GetStartAndEndSprinkler(sprinklers);
		FamilyInstance firstSp = list2Sprinklers.Item1;
		FamilyInstance lastSp = list2Sprinklers.Item2;
		XYZ offsetPoint1 = GetOrigin(firstSp) - new XYZ(0.0, 0.0, zOffset);
		XYZ offsetPoint2 = GetOrigin(lastSp) - new XYZ(0.0, 0.0, zOffset);
		Plane plane = Plane.CreateByNormalAndOrigin(mainLine.Direction, offsetPoint1);
		XYZ intersectionPoint = LineIntersectPlane(mainLine, plane);
		XYZ startPoint = new XYZ(intersectionPoint.X, intersectionPoint.Y, offsetPoint1.Z);
		XYZ endPoint = offsetPoint1;
		FamilyInstance endSprinkler = firstSp;
		double distance1 = startPoint.DistanceTo(offsetPoint1);
		double distance2 = startPoint.DistanceTo(offsetPoint2);
		if (distance1 < distance2)
		{
			endPoint = offsetPoint2;
			endSprinkler = lastSp;
		}
		TransactionGroup tg = new TransactionGroup(doc, "Connect Upright Sprinkler Type 3");
		try
		{
			tg.Start();
			Pipe horizontalPipe = null;
			Transaction t = new Transaction(doc, "create pipe1");
			try
			{
				try { t.GetFailureHandlingOptions().SetFailuresPreprocessor(new SuppressAllWarnings()); } catch { }
				t.Start();
				horizontalPipe = Pipe.Create(doc, systemTypeId, ((Element)pipeType).Id, levelId, startPoint, endPoint);
				((Element)horizontalPipe).get_Parameter((BuiltInParameter)(-1140225)).Set(pipeDiameterFeet);
				t.Commit();
			}
			finally
			{
				((IDisposable)t)?.Dispose();
			}
			List<(Pipe, XYZ)> listPipes1 = new List<(Pipe, XYZ)>();
			List<(Pipe, XYZ)> listPipes2 = new List<(Pipe, XYZ)>();
			Transaction t2 = new Transaction(doc, "create pipe2");
			try
			{
				try { t2.GetFailureHandlingOptions().SetFailuresPreprocessor(new SuppressAllWarnings()); } catch { }
				t2.Start();
				foreach (FamilyInstance sprinkler in sprinklers)
				{
					Connector connector = GetSprinklerConnector(sprinkler);
					XYZ bottomPoint = connector.Origin - new XYZ(0.0, 0.0, zOffset);
					Pipe verticalPipe = Pipe.Create(doc, systemTypeId, ((Element)pipeType).Id, levelId, connector.Origin, bottomPoint);
					((Element)verticalPipe).get_Parameter((BuiltInParameter)(-1140225)).Set(pipeDiameterFeet);
					Connector cn0 = FindConnectorAtPoint(verticalPipe, connector.Origin);
					if (cn0 != null)
					{
						cn0.ConnectTo(connector);
					}
					if (((Element)sprinkler).Id == ((Element)endSprinkler).Id)
					{
						listPipes1.Add((verticalPipe, bottomPoint));
					}
					else
					{
						listPipes2.Add((verticalPipe, bottomPoint));
					}
				}
				if (listPipes1.Any())
				{
					Pipe verPipe1 = listPipes1[0].Item1;
					XYZ point1 = listPipes1[0].Item2;
					Connector cn1 = FindConnectorAtPoint(horizontalPipe, point1);
					Connector cn2 = FindConnectorAtPoint(verPipe1, point1);
					doc.Create.NewElbowFitting(cn1, cn2);
				}
				t2.Commit();
			}
			finally
			{
				((IDisposable)t2)?.Dispose();
			}
			List<XYZ> splitPoints = listPipes2.Select(((Pipe, XYZ) x) => x.Item2).ToList();
			List<Pipe> listNewPipes = SplitPipeAtPoints(doc, horizontalPipe, splitPoints);
			Transaction t3 = new Transaction(doc, "Create tee");
			try
			{
				try { t3.GetFailureHandlingOptions().SetFailuresPreprocessor(new SuppressAllWarnings()); } catch { }
				t3.Start();
				for (int i = 0; i < listPipes2.Count; i++)
				{
					try
					{
						Pipe verticalPipe2 = listPipes2[i].Item1;
						XYZ point2 = listPipes2[i].Item2;
						List<Pipe> list2Pipe = Find2PipesAtPoint(listNewPipes, point2);
						if (list2Pipe.Count == 2)
						{
							CreateTeeFittingAtPoint(list2Pipe[0], list2Pipe[1], verticalPipe2, point2);
						}
					}
					catch
					{
					}
				}
				List<Pipe> findPipes = Find2PipesAtPoint(listNewPipes, startPoint);
				if (findPipes.Any())
				{
					Pipe verticalPipe3 = Pipe.Create(doc, systemTypeId, ((Element)pipeType).Id, levelId, intersectionPoint, startPoint);
					((Element)verticalPipe3).get_Parameter((BuiltInParameter)(-1140225)).Set(pipeDiameterFeet);
					Connector cn3 = FindConnectorAtPoint(findPipes[0], startPoint);
					Connector cn4 = FindConnectorAtPoint(verticalPipe3, startPoint);
					doc.Create.NewElbowFitting(cn3, cn4);
					SplitPipeAndCreateTee(doc, mainPipe, verticalPipe3, intersectionPoint);
				}
				t3.Commit();
			}
			finally
			{
				((IDisposable)t3)?.Dispose();
			}
			tg.Assimilate();
		}
		catch (Exception)
		{
			if (tg.HasStarted()) tg.RollBack();
			throw;
		}
		finally
		{
			((IDisposable)tg)?.Dispose();
		}
		return Result.Succeeded;
	}

	private Connector GetSprinklerConnector(FamilyInstance sprinkler)
	{
		MEPModel mepModel = sprinkler.MEPModel;
		if (mepModel == null)
		{
			return null;
		}
		ConnectorManager connectorManager = mepModel.ConnectorManager;
		if (connectorManager == null)
		{
			return null;
		}
		ConnectorSet connectorSet = connectorManager.UnusedConnectors;
		if (connectorSet == null || connectorSet.Size == 0)
		{
			connectorSet = connectorManager.Connectors;
		}
		return ((IEnumerable)connectorSet)?.Cast<Connector>().FirstOrDefault();
	}

	private XYZ GetOrigin(FamilyInstance sprinkler)
	{
		Connector connector = GetSprinklerConnector(sprinkler);
		return ((connector != null) ? connector.Origin : null) ?? XYZ.Zero;
	}

	private Connector FindConnectorAtPoint(Pipe pipe, XYZ point)
	{
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Expected O, but got Unknown
		if (pipe == null || point == null)
		{
			return null;
		}
		ConnectorManager connectorManager = ((MEPCurve)pipe).ConnectorManager;
		ConnectorSet cns = ((connectorManager != null) ? connectorManager.Connectors : null);
		if (cns == null)
		{
			return null;
		}
		foreach (Connector item in cns)
		{
			Connector cn = item;
			double dis = Math.Round(cn.Origin.DistanceTo(point) * 304.8, 3);
			if (dis == 0.0)
			{
				return cn;
			}
		}
		return null;
	}

	private XYZ LineIntersectPlane(Line line, Plane plane)
	{
		XYZ planeNormal = plane.Normal;
		XYZ planeOrigin = plane.Origin;
		XYZ sp = ((Curve)line).GetEndPoint(0);
		XYZ ep = ((Curve)line).GetEndPoint(1);
		XYZ normalize = (ep - sp).Normalize();
		double distance = (planeNormal.DotProduct(planeOrigin) - planeNormal.DotProduct(sp)) / planeNormal.DotProduct(normalize);
		return sp + distance * normalize;
	}

	private List<Pipe> Find2PipesAtPoint(List<Pipe> listNewPipes, XYZ point)
	{
		return listNewPipes.Where((Pipe pipe) => FindConnectorAtPoint(pipe, point) != null).ToList();
	}

	private FamilyInstance CreateTeeFittingAtPoint(Pipe pipe1, Pipe pipe2, Pipe pipe3, XYZ point)
	{
		Connector cn1 = FindConnectorAtPoint(pipe1, point);
		Connector cn2 = FindConnectorAtPoint(pipe2, point);
		Connector cn3 = FindConnectorAtPoint(pipe3, point);
		if (cn1 == null || cn2 == null || cn3 == null)
		{
			return null;
		}
		return ((Element)pipe1).Document.Create.NewTeeFitting(cn1, cn2, cn3);
	}

	private List<Pipe> SplitPipeAtPoints(Document doc, Pipe pipe, List<XYZ> listPoints)
	{
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Expected O, but got Unknown
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		List<Pipe> listNewPipes = new List<Pipe> { pipe };
		if (listPoints == null || listPoints.Count == 0)
		{
			return listNewPipes;
		}
		Location location = ((Element)pipe).Location;
		LocationCurve locationCurve = (LocationCurve)(object)((location is LocationCurve) ? location : null);
		Line pipeLine = default(Line);
		ref Line reference = ref pipeLine;
		Curve obj = ((locationCurve != null) ? locationCurve.Curve : null);
		reference = (Line)(object)((obj is Line) ? obj : null);
		if ((GeometryObject)(object)pipeLine == (GeometryObject)null)
		{
			return listNewPipes;
		}
		List<XYZ> sortedPoints = listPoints.OrderBy((XYZ p) => ((Curve)pipeLine).Project(p).Parameter).ToList();
		Transaction t = new Transaction(doc, "Split Pipe");
		try
		{
			try { t.GetFailureHandlingOptions().SetFailuresPreprocessor(new SuppressAllWarnings()); } catch { }
				t.Start();
			foreach (XYZ point in sortedPoints)
			{
				try
				{
					ElementId newId = PlumbingUtils.BreakCurve(doc, ((Element)pipe).Id, point);
					Element element = doc.GetElement(newId);
					Pipe newPipe = (Pipe)(object)((element is Pipe) ? element : null);
					if (newPipe != null)
					{
						listNewPipes.Add(newPipe);
					}
				}
				catch
				{
				}
			}
			t.Commit();
		}
		finally
		{
			((IDisposable)t)?.Dispose();
		}
		return listNewPipes;
	}

	private void SplitPipeAndCreateTee(Document doc, Pipe mainPipe, Pipe verPipe, XYZ point)
	{
		ElementId newId = PlumbingUtils.BreakCurve(doc, ((Element)mainPipe).Id, point);
		Element element = doc.GetElement(newId);
		Pipe newPipe = (Pipe)(object)((element is Pipe) ? element : null);
		CreateTeeFittingAtPoint(mainPipe, newPipe, verPipe, point);
	}

	private (FamilyInstance, FamilyInstance) GetStartAndEndSprinkler(List<FamilyInstance> sprinklers)
	{
		FamilyInstance first = null;
		FamilyInstance last = null;
		double maxDistance = 0.0;
		for (int i = 0; i < sprinklers.Count; i++)
		{
			for (int j = i + 1; j < sprinklers.Count; j++)
			{
				double distance = GetOrigin(sprinklers[i]).DistanceTo(GetOrigin(sprinklers[j]));
				if (distance > maxDistance)
				{
					maxDistance = distance;
					first = sprinklers[i];
					last = sprinklers[j];
				}
			}
		}
		return (first, last);
	}

	private Connector GetUnusedConnector(FamilyInstance fitting)
	{
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Expected O, but got Unknown
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Invalid comparison between Unknown and I4
		object obj;
		if (fitting == null)
		{
			obj = null;
		}
		else
		{
			MEPModel mEPModel = fitting.MEPModel;
			if (mEPModel == null)
			{
				obj = null;
			}
			else
			{
				ConnectorManager connectorManager = mEPModel.ConnectorManager;
				obj = ((connectorManager != null) ? connectorManager.Connectors : null);
			}
		}
		if (obj == null)
		{
			return null;
		}
		ConnectorSet connectorSet = fitting.MEPModel.ConnectorManager.Connectors;
		foreach (Connector item in connectorSet)
		{
			Connector connector = item;
			if (!connector.IsConnected && (int)connector.Domain == 3)
			{
				return connector;
			}
		}
		return null;
	}

	private bool ConnectElementToUnusedElbow_Silent(Document doc, FamilyInstance elbow, FamilyInstance sprinkler)
	{
		try
		{
			object obj;
			if (elbow == null)
			{
				obj = null;
			}
			else
			{
				MEPModel mEPModel = elbow.MEPModel;
				if (mEPModel == null)
				{
					obj = null;
				}
				else
				{
					ConnectorManager connectorManager = mEPModel.ConnectorManager;
					obj = ((connectorManager != null) ? ((IEnumerable)connectorManager.Connectors).Cast<Connector>().FirstOrDefault((Connector c) => !c.IsConnected) : null);
				}
			}
			Connector unusedElbowConnector = (Connector)obj;
			object obj2;
			if (sprinkler == null)
			{
				obj2 = null;
			}
			else
			{
				MEPModel mEPModel2 = sprinkler.MEPModel;
				if (mEPModel2 == null)
				{
					obj2 = null;
				}
				else
				{
					ConnectorManager connectorManager2 = mEPModel2.ConnectorManager;
					obj2 = ((connectorManager2 != null) ? ((IEnumerable)connectorManager2.Connectors).Cast<Connector>().FirstOrDefault() : null);
				}
			}
			Connector sprinklerConnector = (Connector)obj2;
			if (unusedElbowConnector == null || sprinklerConnector == null)
			{
				return false;
			}
			XYZ translationVector = unusedElbowConnector.Origin.Subtract(sprinklerConnector.Origin);
			ElementTransformUtils.MoveElement(doc, ((Element)sprinkler).Id, translationVector);
			sprinklerConnector.ConnectTo(unusedElbowConnector);
			return true;
		}
		catch
		{
			return false;
		}
	}

	private (XYZ, Pipe) GetIntersectionPipe(Pipe mainPipe, List<Pipe> listNewPipes)
	{
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		Location location = ((Element)mainPipe).Location;
		Location obj = ((location is LocationCurve) ? location : null);
		Curve obj2 = ((obj != null) ? ((LocationCurve)obj).Curve : null);
		Line mainLine = (Line)(object)((obj2 is Line) ? obj2 : null);
		if ((GeometryObject)(object)mainLine == (GeometryObject)null)
		{
			return (null, null);
		}
		Line flatMainLine = FlatLine(mainLine);
		IntersectionResultArray array = default(IntersectionResultArray);
		foreach (Pipe newPipe in listNewPipes)
		{
			Location location2 = ((Element)newPipe).Location;
			Location obj3 = ((location2 is LocationCurve) ? location2 : null);
			Curve obj4 = ((obj3 != null) ? ((LocationCurve)obj3).Curve : null);
			Line newLine = (Line)(object)((obj4 is Line) ? obj4 : null);
			if (!((GeometryObject)(object)newLine == (GeometryObject)null))
			{
				Line flatNewLine = FlatLine(newLine);
				((Curve)flatMainLine).Intersect((Curve)(object)flatNewLine, out array);
				if (array != null && array.Size == 1)
				{
					return (array.get_Item(0).XYZPoint, newPipe);
				}
			}
		}
		return (null, null);
	}

	private Line FlatLine(Line originalLine)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Expected O, but got Unknown
		//IL_004a: Expected O, but got Unknown
		XYZ sp = ((Curve)originalLine).GetEndPoint(0);
		XYZ ep = ((Curve)originalLine).GetEndPoint(1);
		return Line.CreateBound(new XYZ(sp.X, sp.Y, 0.0), new XYZ(ep.X, ep.Y, 0.0));
	}
}
