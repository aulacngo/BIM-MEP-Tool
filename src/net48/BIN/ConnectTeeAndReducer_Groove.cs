using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class ConnectTeeAndReducer_Groove : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Expected O, but got Unknown
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		ConnectTeeAndReducerWindow window = new ConnectTeeAndReducerWindow();
		if (window.ShowDialog() == true)
		{
			try
			{
				IList<Reference> pickedRefs = uidoc.Selection.PickObjects((ObjectType)1, (ISelectionFilter)(object)new SelectPipes(), "Chon cac doan ong can xoa de noi fitting (nhan Finish)");
				if (pickedRefs == null || pickedRefs.Count == 0)
				{
					return Result.Cancelled;
				}
				Transaction trans = new Transaction(uidoc.Document, "Connect Multiple Fittings");
				try
				{
					try { trans.GetFailureHandlingOptions().SetFailuresPreprocessor(new SuppressAllWarnings()); } catch { }
					trans.Start();
					foreach (Reference rf in pickedRefs)
					{
						Element element = uidoc.Document.GetElement(rf);
						Pipe pipe = (Pipe)(object)((element is Pipe) ? element : null);
						ProcessPipeConnection(uidoc.Document, pipe);
					}
					trans.Commit();
				}
				finally
				{
					((IDisposable)trans)?.Dispose();
				}
				return Result.Succeeded;
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
		return Result.Cancelled;
	}

	private void ProcessPipeConnection(Document doc, Pipe pipe)
	{
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Expected O, but got Unknown
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Expected O, but got Unknown
		//IL_0198: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Expected O, but got Unknown
		//IL_032a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0331: Expected O, but got Unknown
		//IL_01bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c2: Expected O, but got Unknown
		if (pipe == null)
		{
			return;
		}
		ConnectorManager pipeConnManager = ((MEPCurve)pipe).ConnectorManager;
		List<FamilyInstance> fittings = new List<FamilyInstance>();
		List<Connector> fittingConnectors = new List<Connector>();
		List<Connector> pipeConnectors = new List<Connector>();
		foreach (Connector connector in pipeConnManager.Connectors)
		{
			Connector pipeConn = connector;
			foreach (Connector allRef in pipeConn.AllRefs)
			{
				Connector connectedConn = allRef;
				Element owner = connectedConn.Owner;
				FamilyInstance fitting = (FamilyInstance)(object)((owner is FamilyInstance) ? owner : null);
				if (fitting != null)
				{
					fittings.Add(fitting);
					fittingConnectors.Add(connectedConn);
					pipeConnectors.Add(pipeConn);
				}
			}
		}
		if (fittings.Count != 2)
		{
			return;
		}
		FamilyInstance flangeWithTee = null;
		FamilyInstance flangeWithReducer = null;
		Connector connFlangeWithTee = null;
		Connector connFlangeWithReducer = null;
		FamilyInstance reducer = null;
		Connector connReducerToFlange = null;
		for (int i = 0; i < fittings.Count; i++)
		{
			FamilyInstance flange = fittings[i];
			if (IsConnectedToCategory(flange, (BuiltInCategory)(-2008049), 3))
			{
				flangeWithTee = flange;
				connFlangeWithTee = fittingConnectors[i];
			}
			else
			{
				if (!IsConnectedToCategory(flange, (BuiltInCategory)(-2008049), 2))
				{
					continue;
				}
				flangeWithReducer = flange;
				connFlangeWithReducer = fittingConnectors[i];
				foreach (Connector connector2 in flange.MEPModel.ConnectorManager.Connectors)
				{
					Connector conn = connector2;
					foreach (Connector allRef2 in conn.AllRefs)
					{
						Connector refConn = allRef2;
						Element owner2 = refConn.Owner;
						FamilyInstance fi = (FamilyInstance)(object)((owner2 is FamilyInstance) ? owner2 : null);
						if (fi == null)
						{
							continue;
						}
						MEPModel mEPModel = fi.MEPModel;
						if (mEPModel != null)
						{
							ConnectorManager connectorManager = mEPModel.ConnectorManager;
							if (((connectorManager != null) ? new int?(connectorManager.Connectors.Size) : ((int?)null)) == 2 && ((Element)fi).Id != ((Element)pipe).Id)
							{
								reducer = fi;
								connReducerToFlange = refConn;
								break;
							}
						}
					}
					if (reducer != null)
					{
						break;
					}
				}
			}
		}
		if (flangeWithTee == null || flangeWithReducer == null || reducer == null)
		{
			return;
		}
		doc.Delete(((Element)pipe).Id);
		doc.Delete(((Element)flangeWithReducer).Id);
		XYZ translation = connFlangeWithTee.Origin - connReducerToFlange.Origin;
		ElementTransformUtils.MoveElement(doc, ((Element)reducer).Id, translation);
		foreach (Connector connector3 in reducer.MEPModel.ConnectorManager.Connectors)
		{
			Connector conn2 = connector3;
			if (conn2.Origin.IsAlmostEqualTo(connFlangeWithTee.Origin))
			{
				connFlangeWithTee.ConnectTo(conn2);
				break;
			}
		}
	}

	private bool IsConnectedToCategory(FamilyInstance fitting, BuiltInCategory category, int connectorCount)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Expected O, but got Unknown
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Expected O, but got Unknown
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Invalid comparison between I4 and Unknown
		foreach (Connector connector in fitting.MEPModel.ConnectorManager.Connectors)
		{
			Connector conn = connector;
			foreach (Connector allRef in conn.AllRefs)
			{
				Connector refConn = allRef;
				if (refConn.Owner.Category != null && refConn.Owner.Category.GetIdInt() == (int)category)
				{
					Element owner = refConn.Owner;
					FamilyInstance fi = (FamilyInstance)(object)((owner is FamilyInstance) ? owner : null);
					if (fi != null && fi.MEPModel.ConnectorManager.Connectors.Size == connectorCount)
					{
						return true;
					}
				}
			}
		}
		return false;
	}
}
