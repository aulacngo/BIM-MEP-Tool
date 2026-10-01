using Document = Autodesk.Revit.DB.Document;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.Creation;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class ReplacePipeWithNipple : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_0472: Unknown result type (might be due to invalid IL or missing references)
		//IL_0476: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Expected O, but got Unknown
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_044a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0461: Unknown result type (might be due to invalid IL or missing references)
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		//IL_017a: Expected O, but got Unknown
		//IL_0193: Unknown result type (might be due to invalid IL or missing references)
		//IL_019a: Expected O, but got Unknown
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;
		try
		{
			SelectFamilyWindow win = new SelectFamilyWindow(doc);
			if (win.ShowDialog() != true)
			{
				return Result.Cancelled;
			}
			FamilySymbol unionSymbol = win.SelectedSymbol;
			if (unionSymbol == null)
			{
				return Result.Failed;
			}
			ISelectionFilter pipeFilter = (ISelectionFilter)(object)new PipeSelectionFilter();
			IList<Reference> selectedRefs;
			try
			{
				selectedRefs = uidoc.Selection.PickObjects((ObjectType)1, pipeFilter, "Quet chon cac doan ong nuoc can thay the (nhan Finish)");
			}
			catch (Autodesk.Revit.Exceptions.OperationCanceledException)
			{
				return Result.Cancelled;
			}
			Transaction trans = new Transaction(doc, "Replace Pipe with Nipple");
			try
			{
				try { trans.GetFailureHandlingOptions().SetFailuresPreprocessor(new SuppressAllWarnings()); } catch { }
				trans.Start();
				if (!unionSymbol.IsActive)
				{
					unionSymbol.Activate();
				}
				Pipe pipe = default(Pipe);
				foreach (Reference rf in selectedRefs)
				{
					ref Pipe reference = ref pipe;
					Element element = doc.GetElement(rf);
					reference = (Pipe)(object)((element is Pipe) ? element : null);
					if (pipe == null)
					{
						continue;
					}
					Location location = ((Element)pipe).Location;
					LocationCurve locationCurve = (LocationCurve)(object)((location is LocationCurve) ? location : null);
					Curve curve = locationCurve.Curve;
					Line pipeLine = (Line)(object)((curve is Line) ? curve : null);
					XYZ middlePoint = ((Curve)pipeLine).Evaluate(0.5, true);
					double pipeSize = ((Element)pipe).get_Parameter((BuiltInParameter)(-1140225)).AsDouble();
					FamilyInstance tee = null;
					FamilyInstance reducer = null;
					foreach (Connector connector in ((MEPCurve)pipe).ConnectorManager.Connectors)
					{
						Connector c2 = connector;
						foreach (Connector allRef in c2.AllRefs)
						{
							Connector refConn = allRef;
							Element owner = refConn.Owner;
							FamilyInstance fi = (FamilyInstance)(object)((owner is FamilyInstance) ? owner : null);
							if (fi != null)
							{
								int count = fi.MEPModel.ConnectorManager.Connectors.Size;
								if (count >= 3)
								{
									tee = fi;
								}
								else if (count == 2)
								{
									reducer = fi;
								}
							}
						}
					}
					if (tee == null || reducer == null)
					{
						continue;
					}
					Connector teeConnToPipe = ((IEnumerable)tee.MEPModel.ConnectorManager.Connectors).Cast<Connector>().FirstOrDefault((Connector c) => IsConnectedTo(c, ((Element)pipe).Id));
					Connector redConnToPipe = ((IEnumerable)reducer.MEPModel.ConnectorManager.Connectors).Cast<Connector>().FirstOrDefault((Connector c) => IsConnectedTo(c, ((Element)pipe).Id));
					if (teeConnToPipe == null || redConnToPipe == null)
					{
						continue;
					}
					XYZ teePoint = teeConnToPipe.Origin;
					XYZ redPoint = redConnToPipe.Origin;
					XYZ actualDirection = (redPoint - teePoint).Normalize();
					doc.Delete(((Element)pipe).Id);
					doc.Regenerate();
					FamilyInstance unionInst = ((ItemFactoryBase)doc.Create).NewFamilyInstance(middlePoint, unionSymbol, actualDirection, (Element)null, (StructuralType)0);
					Parameter pSize = ((Element)unionInst).LookupParameter("Nominal Diameter") ?? ((Element)unionInst).get_Parameter((BuiltInParameter)(-1140225));
					if (pSize != null)
					{
						pSize.Set(pipeSize);
					}
					doc.Regenerate();
					Connector unionConn1 = GetClosestConnector(unionInst, teePoint, openOnly: false);
					if (unionConn1 != null)
					{
						ElementTransformUtils.MoveElement(doc, ((Element)unionInst).Id, teePoint - unionConn1.Origin);
						doc.Regenerate();
						unionConn1 = GetClosestConnector(unionInst, teePoint, openOnly: false);
						Connector teeOpen = GetOpenConnector(tee);
						if (teeOpen != null)
						{
							unionConn1.ConnectTo(teeOpen);
						}
					}
					doc.Regenerate();
					Connector unionConn2 = GetClosestConnector(unionInst, redPoint, openOnly: true);
					if (unionConn2 == null)
					{
						continue;
					}
					Connector redOpen = GetOpenConnector(reducer);
					if (redOpen != null)
					{
						ElementTransformUtils.MoveElement(doc, ((Element)reducer).Id, unionConn2.Origin - redOpen.Origin);
						doc.Regenerate();
						redOpen = GetOpenConnector(reducer);
						if (redOpen != null)
						{
							redOpen.ConnectTo(unionConn2);
						}
					}
				}
				trans.Commit();
			}
			finally
			{
				((IDisposable)trans)?.Dispose();
			}
			return Result.Succeeded;
		}
		catch (Exception ex)
		{
			message = ex.Message;
			return Result.Failed;
		}
	}

	private bool IsConnectedTo(Connector conn, ElementId id)
	{
		return ((IEnumerable)conn.AllRefs).Cast<Connector>().Any((Connector c) => c.Owner.Id == id);
	}

	private Connector GetOpenConnector(FamilyInstance inst)
	{
		MEPModel mEPModel = inst.MEPModel;
		return (mEPModel != null) ? ((IEnumerable)mEPModel.ConnectorManager.Connectors).Cast<Connector>().FirstOrDefault((Connector c) => !c.IsConnected) : null;
	}

	private Connector GetClosestConnector(FamilyInstance inst, XYZ targetPoint, bool openOnly)
	{
		MEPModel mEPModel = inst.MEPModel;
		IEnumerable<Connector> connectors = ((mEPModel != null) ? ((IEnumerable)mEPModel.ConnectorManager.Connectors).Cast<Connector>() : null);
		if (openOnly)
		{
			connectors = connectors?.Where((Connector c) => !c.IsConnected);
		}
		return connectors?.OrderBy((Connector c) => c.Origin.DistanceTo(targetPoint)).FirstOrDefault();
	}
}
