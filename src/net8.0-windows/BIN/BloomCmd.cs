using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class BloomCmd : IExternalCommand
{
	private class ConnectedCurveInfo
	{
		public ElementId TypeId { get; set; } = ElementId.InvalidElementId;

		public ElementId SystemTypeId { get; set; } = ElementId.InvalidElementId;
	}

	private const double PIPE_LENGTH_MM = 200.0;

	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_02e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_02eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_018c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0192: Invalid comparison between Unknown and I4
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Expected O, but got Unknown
		//IL_01b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01be: Invalid comparison between Unknown and I4
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		UIApplication uiApp = commandData.Application;
		UIDocument uidoc = uiApp.ActiveUIDocument;
		Document doc = uidoc.Document;
		try
		{
			double lengthInFeet = 250.0 / 381.0;
			while (true)
			{
				Reference pickedRef;
				try
				{
					pickedRef = uidoc.Selection.PickObject((ObjectType)1, (ISelectionFilter)(object)new FilterBloom(), "Click vào fitting để tạo ống (ESC để thoát)");
				}
				catch
				{
					break;
				}
				Element elem = doc.GetElement(pickedRef);
				if (elem == null)
				{
					continue;
				}
				Transaction t = new Transaction(doc, "Create MEP From Fitting");
				try
				{
					t.Start();
					ConnectorManager cm = null;
					ElementId levelId = elem.LevelId;
					FamilyInstance fi = (FamilyInstance)(object)((elem is FamilyInstance) ? elem : null);
					if (fi != null && fi.MEPModel != null)
					{
						cm = fi.MEPModel.ConnectorManager;
					}
					else
					{
						MEPCurve curve = (MEPCurve)(object)((elem is MEPCurve) ? elem : null);
						if (curve != null)
						{
							cm = curve.ConnectorManager;
						}
					}
					if (cm == null)
					{
						t.RollBack();
						continue;
					}
					List<Connector> unusedConnectors = (from Connector c in (IEnumerable)cm.Connectors
						where !c.IsConnected
						select c).ToList();
					foreach (Connector cn in unusedConnectors)
					{
						ConnectedCurveInfo info = GetInfoFromFitting(doc, elem, cn);
						if (info.TypeId == ElementId.InvalidElementId)
						{
							continue;
						}
						XYZ direction = cn.CoordinateSystem.BasisZ;
						XYZ endPoint = cn.Origin + direction * lengthInFeet;
						try
						{
							if ((int)cn.Domain == 3)
							{
								Pipe newPipe = Pipe.Create(doc, info.TypeId, levelId, cn, endPoint);
								continue;
							}
							if ((int)cn.Domain == 1)
							{
								Duct newDuct = Duct.Create(doc, info.TypeId, levelId, cn, endPoint);
								continue;
							}
							int catId = elem.Category.GetIdInt();
							MEPCurve newCurve = null;
							if (catId == -2008128 || catId == -2008132)
							{
								newCurve = (MEPCurve)(object)Conduit.Create(doc, info.TypeId, cn.Origin, endPoint, levelId);
							}
							else
							{
								newCurve = (MEPCurve)(object)CableTray.Create(doc, info.TypeId, cn.Origin, endPoint, levelId);
								if (newCurve != null)
								{
									SetCableTraySize(newCurve, cn);
								}
							}
							if (newCurve != null)
							{
								Connector newC = GetConnectorAt(newCurve, cn.Origin);
								if (newC != null)
								{
									newC.ConnectTo(cn);
								}
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
			}
			return Result.Succeeded;
		}
		catch (Exception ex)
		{
			message = ex.Message;
			return Result.Failed;
		}
	}

	private ConnectedCurveInfo GetInfoFromFitting(Document doc, Element elem, Connector targetConnector)
	{
		//IL_04d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_04d7: Invalid comparison between Unknown and I4
		//IL_0530: Unknown result type (might be due to invalid IL or missing references)
		//IL_0536: Invalid comparison between Unknown and I4
		//IL_04f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_054f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0345: Unknown result type (might be due to invalid IL or missing references)
		//IL_034c: Expected O, but got Unknown
		//IL_0358: Unknown result type (might be due to invalid IL or missing references)
		//IL_035e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0149: Unknown result type (might be due to invalid IL or missing references)
		//IL_0421: Unknown result type (might be due to invalid IL or missing references)
		//IL_0428: Expected O, but got Unknown
		//IL_038b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0392: Expected O, but got Unknown
		//IL_01e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e8: Expected O, but got Unknown
		//IL_0453: Unknown result type (might be due to invalid IL or missing references)
		//IL_045a: Expected O, but got Unknown
		//IL_0216: Unknown result type (might be due to invalid IL or missing references)
		//IL_021d: Expected O, but got Unknown
		ConnectedCurveInfo info = new ConnectedCurveInfo();
		FamilyInstance fi = (FamilyInstance)(object)((elem is FamilyInstance) ? elem : null);
		if (fi != null && fi.MEPModel != null)
		{
			int catId = elem.Category?.GetIdInt() ?? 0;
			if (catId == -2008049 || catId == -2008055)
			{
				FindSystemFromConnectedPipe(fi, ref info);
				if (info.TypeId == ElementId.InvalidElementId)
				{
					info.TypeId = doc.GetDefaultElementTypeId((ElementTypeGroup)100);
				}
				if (info.SystemTypeId == ElementId.InvalidElementId)
				{
					PipingSystemType def = ((IEnumerable)new FilteredElementCollector(doc).OfClass(typeof(PipingSystemType))).Cast<PipingSystemType>().FirstOrDefault();
					if (def != null)
					{
						info.SystemTypeId = ((Element)def).Id;
					}
				}
				return info;
			}
			if (catId == -2008010 || catId == -2008016)
			{
				FindSystemFromConnectedDuct(fi, ref info);
				if (info.TypeId == ElementId.InvalidElementId)
				{
					info.TypeId = doc.GetDefaultElementTypeId((ElementTypeGroup)97);
				}
				if (info.SystemTypeId == ElementId.InvalidElementId)
				{
					MechanicalSystemType def2 = ((IEnumerable)new FilteredElementCollector(doc).OfClass(typeof(MechanicalSystemType))).Cast<MechanicalSystemType>().FirstOrDefault();
					if (def2 != null)
					{
						info.SystemTypeId = ((Element)def2).Id;
					}
				}
				return info;
			}
			if (catId == -2008126)
			{
				MEPModel mEPModel = fi.MEPModel;
				if (((mEPModel != null) ? mEPModel.ConnectorManager : null) != null)
				{
					foreach (Connector connector in fi.MEPModel.ConnectorManager.Connectors)
					{
						Connector cn = connector;
						if (!cn.IsConnected)
						{
							continue;
						}
						foreach (Connector allRef in cn.AllRefs)
						{
							Connector refCn = allRef;
							Element owner = refCn.Owner;
							CableTray refCT = (CableTray)(object)((owner is CableTray) ? owner : null);
							if (refCT != null)
							{
								info.TypeId = ((Element)refCT).GetTypeId();
								break;
							}
						}
						if (!(info.TypeId != ElementId.InvalidElementId))
						{
							continue;
						}
						break;
					}
				}
				if (info.TypeId == ElementId.InvalidElementId)
				{
					info.TypeId = doc.GetDefaultElementTypeId((ElementTypeGroup)122);
				}
				return info;
			}
		}
		ConnectorManager cmFallback = null;
		FamilyInstance fi2 = (FamilyInstance)(object)((elem is FamilyInstance) ? elem : null);
		if (fi2 != null && fi2.MEPModel != null)
		{
			cmFallback = fi2.MEPModel.ConnectorManager;
		}
		else
		{
			MEPCurve curveElem = (MEPCurve)(object)((elem is MEPCurve) ? elem : null);
			if (curveElem != null)
			{
				cmFallback = curveElem.ConnectorManager;
			}
		}
		if (cmFallback != null)
		{
			foreach (Connector connector2 in cmFallback.Connectors)
			{
				Connector cn2 = connector2;
				if (!cn2.IsConnected || cn2.Domain != targetConnector.Domain)
				{
					continue;
				}
				foreach (Connector allRef2 in cn2.AllRefs)
				{
					Connector refCn2 = allRef2;
					Element owner2 = refCn2.Owner;
					MEPCurve c = (MEPCurve)(object)((owner2 is MEPCurve) ? owner2 : null);
					if (c != null)
					{
						FillInfoFromCurve(c, ref info);
						return info;
					}
				}
			}
			foreach (Connector connector3 in cmFallback.Connectors)
			{
				Connector cn3 = connector3;
				if (!cn3.IsConnected)
				{
					continue;
				}
				foreach (Connector allRef3 in cn3.AllRefs)
				{
					Connector refCn3 = allRef3;
					Element owner3 = refCn3.Owner;
					MEPCurve c2 = (MEPCurve)(object)((owner3 is MEPCurve) ? owner3 : null);
					if (c2 != null)
					{
						FillInfoFromCurve(c2, ref info);
						return info;
					}
				}
			}
		}
		if ((int)targetConnector.Domain == 3)
		{
			info.TypeId = doc.GetDefaultElementTypeId((ElementTypeGroup)100);
			PipingSystemType def3 = ((IEnumerable)new FilteredElementCollector(doc).OfClass(typeof(PipingSystemType))).Cast<PipingSystemType>().FirstOrDefault();
			if (def3 != null)
			{
				info.SystemTypeId = ((Element)def3).Id;
			}
		}
		else if ((int)targetConnector.Domain == 1)
		{
			info.TypeId = doc.GetDefaultElementTypeId((ElementTypeGroup)97);
			MechanicalSystemType def4 = ((IEnumerable)new FilteredElementCollector(doc).OfClass(typeof(MechanicalSystemType))).Cast<MechanicalSystemType>().FirstOrDefault();
			if (def4 != null)
			{
				info.SystemTypeId = ((Element)def4).Id;
			}
		}
		else
		{
			int catId2 = elem.Category?.GetIdInt() ?? 0;
			info.TypeId = ((catId2 == -2008128 || catId2 == -2008132) ? doc.GetDefaultElementTypeId((ElementTypeGroup)123) : doc.GetDefaultElementTypeId((ElementTypeGroup)122));
		}
		return info;
	}

	private void FindSystemFromConnectedPipe(FamilyInstance fi, ref ConnectedCurveInfo info)
	{
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Expected O, but got Unknown
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Invalid comparison between Unknown and I4
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Expected O, but got Unknown
		MEPModel mEPModel = fi.MEPModel;
		if (((mEPModel != null) ? mEPModel.ConnectorManager : null) == null)
		{
			return;
		}
		foreach (Connector connector in fi.MEPModel.ConnectorManager.Connectors)
		{
			Connector cn = connector;
			if (!cn.IsConnected || (int)cn.Domain != 3)
			{
				continue;
			}
			foreach (Connector allRef in cn.AllRefs)
			{
				Connector refCn = allRef;
				Element owner = refCn.Owner;
				Pipe refPipe = (Pipe)(object)((owner is Pipe) ? owner : null);
				if (refPipe == null)
				{
					continue;
				}
				if (info.TypeId == ElementId.InvalidElementId)
				{
					info.TypeId = ((Element)refPipe).GetTypeId();
				}
				if (info.SystemTypeId == ElementId.InvalidElementId)
				{
					Parameter obj = ((Element)refPipe).get_Parameter((BuiltInParameter)(-1140334));
					ElementId sysId = ((obj != null) ? obj.AsElementId() : null);
					if (sysId != (ElementId)null && sysId != ElementId.InvalidElementId)
					{
						info.SystemTypeId = sysId;
					}
					else if (((MEPCurve)refPipe).MEPSystem != null)
					{
						info.SystemTypeId = ((Element)((MEPCurve)refPipe).MEPSystem).GetTypeId();
					}
				}
				if (info.TypeId != ElementId.InvalidElementId && info.SystemTypeId != ElementId.InvalidElementId)
				{
					return;
				}
			}
		}
	}

	private void FindSystemFromConnectedDuct(FamilyInstance fi, ref ConnectedCurveInfo info)
	{
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Expected O, but got Unknown
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Invalid comparison between Unknown and I4
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Expected O, but got Unknown
		MEPModel mEPModel = fi.MEPModel;
		if (((mEPModel != null) ? mEPModel.ConnectorManager : null) == null)
		{
			return;
		}
		foreach (Connector connector in fi.MEPModel.ConnectorManager.Connectors)
		{
			Connector cn = connector;
			if (!cn.IsConnected || (int)cn.Domain != 1)
			{
				continue;
			}
			foreach (Connector allRef in cn.AllRefs)
			{
				Connector refCn = allRef;
				Element owner = refCn.Owner;
				Duct refDuct = (Duct)(object)((owner is Duct) ? owner : null);
				if (refDuct == null)
				{
					continue;
				}
				if (info.TypeId == ElementId.InvalidElementId)
				{
					info.TypeId = ((Element)refDuct).GetTypeId();
				}
				if (info.SystemTypeId == ElementId.InvalidElementId)
				{
					Parameter obj = ((Element)refDuct).get_Parameter((BuiltInParameter)(-1140333));
					ElementId sysId = ((obj != null) ? obj.AsElementId() : null);
					if (sysId != (ElementId)null && sysId != ElementId.InvalidElementId)
					{
						info.SystemTypeId = sysId;
					}
					else if (((MEPCurve)refDuct).MEPSystem != null)
					{
						info.SystemTypeId = ((Element)((MEPCurve)refDuct).MEPSystem).GetTypeId();
					}
				}
				if (info.TypeId != ElementId.InvalidElementId && info.SystemTypeId != ElementId.InvalidElementId)
				{
					return;
				}
			}
		}
	}

	private void FillInfoFromCurve(MEPCurve curve, ref ConnectedCurveInfo info)
	{
		info.TypeId = ((Element)curve).GetTypeId();
		Pipe pipe = (Pipe)(object)((curve is Pipe) ? curve : null);
		if (pipe != null)
		{
			ConnectedCurveInfo obj = info;
			Parameter obj2 = ((Element)pipe).get_Parameter((BuiltInParameter)(-1140334));
			obj.SystemTypeId = ((obj2 != null) ? obj2.AsElementId() : null) ?? ElementId.InvalidElementId;
			if (info.SystemTypeId == ElementId.InvalidElementId && ((MEPCurve)pipe).MEPSystem != null)
			{
				info.SystemTypeId = ((Element)((MEPCurve)pipe).MEPSystem).GetTypeId();
			}
			return;
		}
		Duct duct = (Duct)(object)((curve is Duct) ? curve : null);
		if (duct != null)
		{
			ConnectedCurveInfo obj3 = info;
			Parameter obj4 = ((Element)duct).get_Parameter((BuiltInParameter)(-1140333));
			obj3.SystemTypeId = ((obj4 != null) ? obj4.AsElementId() : null) ?? ElementId.InvalidElementId;
			if (info.SystemTypeId == ElementId.InvalidElementId && ((MEPCurve)duct).MEPSystem != null)
			{
				info.SystemTypeId = ((Element)((MEPCurve)duct).MEPSystem).GetTypeId();
			}
		}
	}

	private void SetPipeSize(Pipe pipe, Connector cn)
	{
		try
		{
			double radius = cn.Radius;
			if (!(radius <= 0.0))
			{
				Parameter p = ((Element)pipe).get_Parameter((BuiltInParameter)(-1140225));
				if (p != null && !((APIObject)p).IsReadOnly)
				{
					p.Set(radius * 2.0);
				}
			}
		}
		catch
		{
		}
	}

	private void SetDuctSize(Duct duct, Connector cn)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Invalid comparison between Unknown and I4
		try
		{
			if ((int)cn.Shape == 0)
			{
				double radius = cn.Radius;
				if (!(radius <= 0.0))
				{
					Parameter p = ((Element)duct).get_Parameter((BuiltInParameter)(-1114103));
					if (p != null && !((APIObject)p).IsReadOnly)
					{
						p.Set(radius * 2.0);
					}
				}
				return;
			}
			double w = cn.Width;
			double h = cn.Height;
			if (w > 0.0)
			{
				Parameter p2 = ((Element)duct).get_Parameter((BuiltInParameter)(-1114101));
				if (p2 != null && !((APIObject)p2).IsReadOnly)
				{
					p2.Set(w);
				}
			}
			if (h > 0.0)
			{
				Parameter p3 = ((Element)duct).get_Parameter((BuiltInParameter)(-1114102));
				if (p3 != null && !((APIObject)p3).IsReadOnly)
				{
					p3.Set(h);
				}
			}
		}
		catch
		{
		}
	}

	private void SetCableTraySize(MEPCurve tray, Connector cn)
	{
		try
		{
			double w = cn.Width;
			double h = cn.Height;
			if (w > 0.0)
			{
				Parameter p = ((Element)tray).get_Parameter((BuiltInParameter)(-1140122));
				if (p != null && !((APIObject)p).IsReadOnly)
				{
					p.Set(w);
				}
			}
			if (h > 0.0)
			{
				Parameter p2 = ((Element)tray).get_Parameter((BuiltInParameter)(-1140121));
				if (p2 != null && !((APIObject)p2).IsReadOnly)
				{
					p2.Set(h);
				}
			}
		}
		catch
		{
		}
	}

	private Connector GetConnectorAt(MEPCurve curve, XYZ point)
	{
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Expected O, but got Unknown
		if (((curve != null) ? curve.ConnectorManager : null) == null)
		{
			return null;
		}
		foreach (Connector connector in curve.ConnectorManager.Connectors)
		{
			Connector cn = connector;
			if (cn.Origin.DistanceTo(point) < 0.01)
			{
				return cn;
			}
		}
		return null;
	}
}
