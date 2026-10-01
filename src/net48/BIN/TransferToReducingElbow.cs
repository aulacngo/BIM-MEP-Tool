using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class TransferToReducingElbow : IExternalCommand
{
	private const double TargetDnMm = 15.0;

	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0235: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Expected O, but got Unknown
		//IL_018d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Expected O, but got Unknown
		//IL_01a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_022a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0231: Unknown result type (might be due to invalid IL or missing references)
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;
		TransferToReducingElbowWindow dlg = new TransferToReducingElbowWindow();
		try
		{
			WindowInteropHelper helper = new WindowInteropHelper(dlg)
			{
				Owner = commandData.Application.MainWindowHandle
			};
		}
		catch
		{
		}
		if (dlg.ShowDialog() != true)
		{
			return Result.Cancelled;
		}
		IList<Reference> picked;
		try
		{
			picked = uidoc.Selection.PickObjects((ObjectType)1, (ISelectionFilter)(object)new SprinklerSelectFilter(), "Quet chon cac dau phun Sprinkler (nhan Finish)");
		}
		catch (Autodesk.Revit.Exceptions.OperationCanceledException)
		{
			return Result.Cancelled;
		}
		int ok = 0;
		int fail = 0;
		List<string> logs = new List<string>();
		TransactionGroup tg = new TransactionGroup(doc, "Rut gon Sprinkler (Optimized)");
		try
		{
			tg.Start();
			Transaction t = new Transaction(doc, "Shorten sprinklers");
			try
			{
				try { t.GetFailureHandlingOptions().SetFailuresPreprocessor(new SuppressAllWarnings()); } catch { }
				t.Start();
				foreach (Reference r in picked)
				{
					Element element = doc.GetElement(r);
					FamilyInstance spr = (FamilyInstance)(object)((element is FamilyInstance) ? element : null);
					if (spr == null)
					{
						fail++;
						continue;
					}
					try
					{
						if (ProcessOne(doc, spr, out var reason))
						{
							ok++;
							continue;
						}
						fail++;
						logs.Add($"{((Element)(object)spr).GetIdInt()}: {reason}");
					}
					catch (Exception ex)
					{
						fail++;
						logs.Add($"{((Element)(object)spr).GetIdInt()}: EX - {ex.Message}");
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
		new TaskDialog("Result")
		{
			MainInstruction = $"Done. Success: {ok}, Failed: {fail}",
			MainContent = ((logs.Count == 0) ? "" : (string.Join(Environment.NewLine, logs.Take(40)) + ((logs.Count > 40) ? "\n..." : "")))
		}.Show();
		return Result.Succeeded;
	}

	private bool ProcessOne(Document doc, FamilyInstance sprinkler, out string reason)
	{
		reason = "";
		Connector sprConn = GetPrimaryPipingConnector(sprinkler);
		if (sprConn == null)
		{
			reason = "No sprinkler piping connector";
			return false;
		}
		if (!sprConn.IsConnected)
		{
			reason = "Sprinkler is not connected";
			return false;
		}
		if (!TryGetConnectedReducer(sprConn, out var reducer))
		{
			reason = "Cannot find reducer connected to sprinkler";
			return false;
		}
		Connector reducerPipeConn = GetReducerPipeConnector(reducer);
		if (reducerPipeConn == null)
		{
			reason = "Reducer has no connector connected to Pipe";
			return false;
		}
		Pipe pipe = (from Connector c in (IEnumerable)reducerPipeConn.AllRefs
			select c.Owner).OfType<Pipe>().FirstOrDefault();
		if (pipe == null)
		{
			reason = "Cannot resolve Pipe from reducer";
			return false;
		}
		if (!TryGetOtherEndFitting(pipe, reducer, out var elbow))
		{
			reason = "Cannot find other-end fitting (elbow)";
			return false;
		}
		if (!TrySetPipeDiameter(pipe, 15.0))
		{
			reason = "Cannot set pipe diameter DN15";
			return false;
		}
		XYZ oldSprOrigin = sprConn.Origin;
		doc.Delete(((Element)pipe).Id);
		doc.Delete(((Element)reducer).Id);
		doc.Regenerate();
		Connector openElbowConn = GetOpenRoundPipingConnector(elbow);
		if (openElbowConn == null)
		{
			reason = "No open elbow connector after deletion";
			return false;
		}
		if (!TrySetConnectorDiameter(openElbowConn, 15.0))
		{
			reason = "Cannot set elbow open connector to DN15 (Radius set failed)";
			return false;
		}
		doc.Regenerate();
		XYZ targetPoint = openElbowConn.Origin;
		XYZ translation = targetPoint - oldSprOrigin;
		if (translation.GetLength() > 1E-09)
		{
			ElementTransformUtils.MoveElement(doc, ((Element)sprinkler).Id, translation);
		}
		doc.Regenerate();
		Connector finalSprConn = GetPrimaryPipingConnector(sprinkler);
		if (finalSprConn == null)
		{
			reason = "Cannot reacquire sprinkler connector after move";
			return false;
		}
		if (finalSprConn.IsConnected)
		{
			foreach (Connector rc in ((IEnumerable)finalSprConn.AllRefs).Cast<Connector>().ToList())
			{
				try
				{
					finalSprConn.DisconnectFrom(rc);
				}
				catch
				{
				}
			}
		}
		try
		{
			finalSprConn.ConnectTo(openElbowConn);
		}
		catch
		{
			reason = "ConnectTo failed";
			return false;
		}
		return true;
	}

	private static Connector GetPrimaryPipingConnector(FamilyInstance fi)
	{
		object obj;
		if (fi == null)
		{
			obj = null;
		}
		else
		{
			MEPModel mEPModel = fi.MEPModel;
			obj = ((mEPModel != null) ? mEPModel.ConnectorManager : null);
		}
		ConnectorManager cm = (ConnectorManager)obj;
		if (cm == null)
		{
			return null;
		}
		List<Connector> conns = ((IEnumerable)cm.Connectors).Cast<Connector>().ToList();
		return (from c in conns
			where (int)c.Domain == 3
			orderby (int)c.Shape == 0 descending, (int)c.ConnectorType == 1 descending
			select c).FirstOrDefault();
	}

	private static bool TryGetConnectedReducer(Connector sprinklerConn, out FamilyInstance reducer)
	{
		reducer = (from Connector c in (IEnumerable)sprinklerConn.AllRefs
			select c.Owner).OfType<FamilyInstance>().FirstOrDefault(IsTwoConnectorPipeFitting);
		return reducer != null;
	}

	private static bool IsTwoConnectorPipeFitting(FamilyInstance fi)
	{
		object obj;
		if (fi == null)
		{
			obj = null;
		}
		else
		{
			MEPModel mEPModel = fi.MEPModel;
			obj = ((mEPModel != null) ? mEPModel.ConnectorManager : null);
		}
		if (obj == null)
		{
			return false;
		}
		if (((Element)fi).Category == null || ((Element)fi).Category.GetIdInt() != -2008049)
		{
			return false;
		}
		int cnt = ((IEnumerable)fi.MEPModel.ConnectorManager.Connectors).Cast<Connector>().Count((Connector c) => (int)c.Domain == 3);
		return cnt == 2;
	}

	private static Connector GetReducerPipeConnector(FamilyInstance reducer)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Expected O, but got Unknown
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Invalid comparison between Unknown and I4
		foreach (Connector connector in reducer.MEPModel.ConnectorManager.Connectors)
		{
			Connector c = connector;
			if ((int)c.Domain != 3 || !((IEnumerable)c.AllRefs).Cast<Connector>().Any((Connector r) => r.Owner is Pipe))
			{
				continue;
			}
			return c;
		}
		return null;
	}

	private static bool TryGetOtherEndFitting(Pipe pipe, FamilyInstance reducer, out FamilyInstance fitting)
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Expected O, but got Unknown
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Invalid comparison between Unknown and I4
		fitting = null;
		foreach (Connector connector in ((MEPCurve)pipe).ConnectorManager.Connectors)
		{
			Connector pConn = connector;
			if ((int)pConn.Domain == 3 && pConn.IsConnected && !((IEnumerable)pConn.AllRefs).Cast<Connector>().Any(delegate(Connector r)
			{
				Element owner = r.Owner;
				return ((owner != null) ? owner.Id : null) == ((Element)reducer).Id;
			}))
			{
				fitting = (from Connector r in (IEnumerable)pConn.AllRefs
					select r.Owner).OfType<FamilyInstance>().FirstOrDefault();
				if (fitting != null)
				{
					return true;
				}
			}
		}
		return false;
	}

	private static bool TrySetPipeDiameter(Pipe pipe, double diameterMm)
	{
		Parameter p = ((Element)pipe).get_Parameter((BuiltInParameter)(-1140225));
		if (p == null || ((APIObject)p).IsReadOnly)
		{
			return false;
		}
		double v = UnitUtils.ConvertToInternalUnits(diameterMm, UnitTypeId.Millimeters);
		if (Math.Abs(p.AsDouble() - v) < 1E-09)
		{
			return true;
		}
		p.Set(v);
		return true;
	}

	private static Connector GetOpenRoundPipingConnector(FamilyInstance fitting)
	{
		object obj;
		if (fitting == null)
		{
			obj = null;
		}
		else
		{
			MEPModel mEPModel = fitting.MEPModel;
			obj = ((mEPModel != null) ? mEPModel.ConnectorManager : null);
		}
		ConnectorManager cm = (ConnectorManager)obj;
		if (cm == null)
		{
			return null;
		}
		return (from Connector c in (IEnumerable)cm.Connectors
			where (int)c.Domain == 3 && (int)c.Shape == 0 && !c.IsConnected
			select c).FirstOrDefault();
	}

	private static bool TrySetConnectorDiameter(Connector conn, double diameterMm)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Invalid comparison between Unknown and I4
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Invalid comparison between Unknown and I4
		if (conn == null)
		{
			return false;
		}
		if ((int)conn.Domain != 3)
		{
			return false;
		}
		if ((int)conn.Shape > 0)
		{
			return false;
		}
		double r = UnitUtils.ConvertToInternalUnits(diameterMm / 2.0, UnitTypeId.Millimeters);
		try
		{
			conn.Radius = r;
			return true;
		}
		catch
		{
			return false;
		}
	}
}
