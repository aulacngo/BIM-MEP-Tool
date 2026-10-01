using System;
using System.Collections;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class Trim3DCmd : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Expected O, but got Unknown
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Invalid comparison between Unknown and I4
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Invalid comparison between Unknown and I4
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Invalid comparison between Unknown and I4
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;
		try
		{
			TaskDialog td = new TaskDialog("BIM TOOL - Trim 3D");
			td.MainInstruction = "Chon phuong thuc ket noi 2 ong khac cao do Z:";
			td.AddCommandLink((TaskDialogCommandLinkId)1001, "Option 1 - Noi dau-dau (2 Elbows)", "2 ống chạy ngang, nối bằng ống đứng + 2 elbow.\nTự động cắt ngắn hoặc kéo dài nếu ống vượt qua / chưa tới điểm giao.");
			td.AddCommandLink((TaskDialogCommandLinkId)1002, "Option 2 - Nhanh cam vao chinh (Elbow 90 deg + Tee)", "Ống nhánh elbow 90° → ống ĐỨNG → cắm vào ống chính tạo Tee.\nHoạt động cả khi nhánh ở trên lẫn dưới ống chính.");
			td.AddCommandLink((TaskDialogCommandLinkId)1003, "Option 3 - Nhanh cam vao chinh (Elbow 45 deg + Tee)", "Ống nhánh elbow 45° → ống XIÊN 45° → cắm vào ống chính tạo Tee.\nHoạt động cả khi nhánh ở trên lẫn dưới ống chính.");
			td.CommonButtons = (TaskDialogCommonButtons)8;
			TaskDialogResult tdResult = td.Show();
			if ((int)tdResult == 2)
			{
				return Result.Cancelled;
			}
			if ((int)tdResult == 1003)
			{
				return ExecuteOption3(uidoc, doc, ref message);
			}
			if ((int)tdResult == 1002)
			{
				return ExecuteOption2(uidoc, doc, ref message);
			}
			return ExecuteOption1(uidoc, doc, ref message);
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

	private Result ExecuteOption1(UIDocument uidoc, Document doc, ref string message)
	{
		//IL_055c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0576: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_05b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_012e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0135: Expected O, but got Unknown
		//IL_0145: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Expected O, but got Unknown
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0403: Unknown result type (might be due to invalid IL or missing references)
		//IL_040a: Expected O, but got Unknown
		//IL_03e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0462: Unknown result type (might be due to invalid IL or missing references)
		//IL_0469: Unknown result type (might be due to invalid IL or missing references)
		//IL_0477: Unknown result type (might be due to invalid IL or missing references)
		//IL_047e: Expected O, but got Unknown
		//IL_0290: Unknown result type (might be due to invalid IL or missing references)
		//IL_0297: Expected O, but got Unknown
		//IL_02a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ae: Expected O, but got Unknown
		//IL_0272: Unknown result type (might be due to invalid IL or missing references)
		//IL_0279: Unknown result type (might be due to invalid IL or missing references)
		//IL_0481: Unknown result type (might be due to invalid IL or missing references)
		//IL_049e: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_04d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_059d: Unknown result type (might be due to invalid IL or missing references)
		//IL_05b4: Unknown result type (might be due to invalid IL or missing references)
		Reference ref1 = uidoc.Selection.PickObject((ObjectType)1, (ISelectionFilter)(object)new Trim3DPipeFilter(), "Trim 3D Opt1 [1/2]: Chọn Pipe hoặc Duct tròn 1");
		Element element = doc.GetElement(ref1);
		MEPCurve pipe1 = element as MEPCurve;
		Reference ref2 = uidoc.Selection.PickObject((ObjectType)1, (ISelectionFilter)(object)new Trim3DPipeFilter(), "Trim 3D Opt1 [2/2]: Chọn Pipe hoặc Duct tròn 2");
		Element element2 = doc.GetElement(ref2);
		MEPCurve pipe2 = element2 as MEPCurve;
		if (!ValidateSupportedPair(pipe1, pipe2, "Option 1", ref message)) return Result.Failed;
		if (((Element)pipe1).Id == ((Element)pipe2).Id)
		{
			TaskDialog.Show("Trim 3D", "Vui long chon 2 ong khac nhau.");
			return Result.Failed;
		}
		Line line1 = GetPipeLine(pipe1);
		Line line2 = GetPipeLine(pipe2);
		if ((GeometryObject)(object)line1 == (GeometryObject)null || (GeometryObject)(object)line2 == (GeometryObject)null)
		{
			TaskDialog.Show("Trim 3D", "Chi ho tro ong thang (straight pipe).");
			return Result.Failed;
		}
		XYZ p1_A = ((Curve)line1).GetEndPoint(0);
		XYZ p1_B = ((Curve)line1).GetEndPoint(1);
		XYZ p2_A = ((Curve)line2).GetEndPoint(0);
		XYZ p2_B = ((Curve)line2).GetEndPoint(1);
		XYZ dir1 = line1.Direction.Normalize();
		XYZ dir2 = line2.Direction.Normalize();
		double Z_ref = (p1_A.Z + p1_B.Z) / 2.0;
		XYZ o1_proj = new XYZ(p1_A.X, p1_A.Y, Z_ref);
		XYZ o2_proj = new XYZ(p2_A.X, p2_A.Y, Z_ref);
		XYZ P_xy = Find2DIntersection(o1_proj, dir1, o2_proj, dir2, Z_ref);
		if (P_xy != null)
		{
			double maxLen = Math.Max(DistXY(p1_A, p1_B), DistXY(p2_A, p2_B));
			double nearDist = Math.Min(Math.Min(DistXY(p1_A, P_xy), DistXY(p1_B, P_xy)), Math.Min(DistXY(p2_A, P_xy), DistXY(p2_B, P_xy)));
			if (maxLen < 1E-06 || nearDist > maxLen * 50.0)
			{
				P_xy = null;
			}
		}
		XYZ end1;
		XYZ end2;
		XYZ P_top;
		XYZ P_bottom;
		if (P_xy != null)
		{
			end1 = ((DistXY(p1_A, P_xy) <= DistXY(p1_B, P_xy)) ? p1_A : p1_B);
			end2 = ((DistXY(p2_A, P_xy) <= DistXY(p2_B, P_xy)) ? p2_A : p2_B);
			double Z1 = end1.Z;
			double Z2 = end2.Z;
			if (Math.Abs(Z1 - Z2) < 0.01)
			{
				TaskDialog.Show("Trim 3D", "Hai đầu ống gần như cùng cao độ Z.\nCông cụ này chỉ dùng cho 2 ống có chênh lệch Z.");
				return Result.Failed;
			}
			P_top = new XYZ(P_xy.X, P_xy.Y, Z1);
			P_bottom = new XYZ(P_xy.X, P_xy.Y, Z2);
			XYZ p_bot_corr = ProjectOnLine(P_bottom, p2_A, dir2);
			if (P_bottom.DistanceTo(p_bot_corr) < 0.05)
			{
				P_bottom = p_bot_corr;
			}
		}
		else
		{
			double dAC = DistXY(p1_A, p2_A);
			double dAD = DistXY(p1_A, p2_B);
			double dBC = DistXY(p1_B, p2_A);
			double dBD = DistXY(p1_B, p2_B);
			double minD = Math.Min(Math.Min(dAC, dAD), Math.Min(dBC, dBD));
			if (Math.Abs(minD - dAC) < 1E-09)
			{
				end1 = p1_A;
				end2 = p2_A;
			}
			else if (Math.Abs(minD - dAD) < 1E-09)
			{
				end1 = p1_A;
				end2 = p2_B;
			}
			else if (Math.Abs(minD - dBC) < 1E-09)
			{
				end1 = p1_B;
				end2 = p2_A;
			}
			else
			{
				end1 = p1_B;
				end2 = p2_B;
			}
			double Z3 = end1.Z;
			double Z4 = end2.Z;
			if (Math.Abs(Z3 - Z4) < 0.01)
			{
				TaskDialog.Show("Trim 3D", "Hai đầu ống gần như cùng cao độ Z.\nCông cụ này chỉ dùng cho 2 ống có chênh lệch Z.");
				return Result.Failed;
			}
			P_top = end1;
			XYZ dropTarget = new XYZ(end1.X, end1.Y, Z4);
			P_bottom = ProjectOnLine(dropTarget, p2_A, dir2);
			double offXY = DistXY(dropTarget, P_bottom);
			if (offXY > 0.5)
			{
				TaskDialog.Show("Trim 3D", $"Hai ống song song và lệch nhau {offXY * 304.8:F0} mm theo XY.\n" + "Không thể tạo ống đứng thẳng. Dùng tool khác để nối offset.");
				return Result.Failed;
			}
		}
		Transaction tr = new Transaction(doc, "Trim 3D – Option 1");
		try
		{
			tr.Start();
			if (!ExtendPipeEndTo(pipe1, end1, P_top))
			{
				tr.RollBack();
				message = "Không thể trim/extend Pipe 1 đến P_top.";
				return Result.Failed;
			}
			if (!ExtendPipeEndTo(pipe2, end2, P_bottom))
			{
				tr.RollBack();
				message = "Không thể trim/extend Pipe 2 đến P_bottom.";
				return Result.Failed;
			}
			doc.Regenerate();
			MEPCurve vertPipe;
			try
			{
				vertPipe = CreateMatchingSegment(doc, pipe1, pipe1, P_top, P_bottom);
				doc.Regenerate();
			}
			catch (Exception ex)
			{
				tr.RollBack();
				message = "Không tạo được ống đứng: " + ex.Message;
				return Result.Failed;
			}
			ConnectWithElbow(doc, pipe1, vertPipe, P_top);
			doc.Regenerate();
			ConnectWithElbow(doc, pipe2, vertPipe, P_bottom);
			tr.Commit();
		}
		finally
		{
			((IDisposable)tr)?.Dispose();
		}
		return Result.Succeeded;
	}

	private Result ExecuteOption2(UIDocument uidoc, Document doc, ref string message)
	{
		//IL_0381: Unknown result type (might be due to invalid IL or missing references)
		//IL_0389: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0541: Unknown result type (might be due to invalid IL or missing references)
		//IL_0418: Unknown result type (might be due to invalid IL or missing references)
		//IL_0435: Unknown result type (might be due to invalid IL or missing references)
		//IL_043c: Expected O, but got Unknown
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_043f: Unknown result type (might be due to invalid IL or missing references)
		//IL_04c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_017d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0184: Expected O, but got Unknown
		//IL_0194: Unknown result type (might be due to invalid IL or missing references)
		//IL_019b: Expected O, but got Unknown
		//IL_015f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0166: Unknown result type (might be due to invalid IL or missing references)
		//IL_04e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_04eb: Expected O, but got Unknown
		//IL_01e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e8: Expected O, but got Unknown
		//IL_0206: Unknown result type (might be due to invalid IL or missing references)
		//IL_020d: Expected O, but got Unknown
		//IL_0231: Unknown result type (might be due to invalid IL or missing references)
		//IL_0238: Expected O, but got Unknown
		//IL_01c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_023b: Unknown result type (might be due to invalid IL or missing references)
		//IL_024a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0251: Expected O, but got Unknown
		//IL_050e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0254: Unknown result type (might be due to invalid IL or missing references)
		//IL_0526: Unknown result type (might be due to invalid IL or missing references)
		//IL_053d: Unknown result type (might be due to invalid IL or missing references)
		//IL_028f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0297: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0308: Unknown result type (might be due to invalid IL or missing references)
		//IL_03de: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e5: Expected O, but got Unknown
		//IL_03e8: Unknown result type (might be due to invalid IL or missing references)
		Reference refBranch = uidoc.Selection.PickObject((ObjectType)1, (ISelectionFilter)(object)new Trim3DPipeFilter(), "Trim 3D Opt2 [1/2]: Chọn nhánh Pipe hoặc Duct tròn");
		Element element = doc.GetElement(refBranch);
		MEPCurve branchPipe = element as MEPCurve;
		Reference refMain = uidoc.Selection.PickObject((ObjectType)1, (ISelectionFilter)(object)new Trim3DPipeFilter(), "Trim 3D Opt2 [2/2]: Chọn chính Pipe hoặc Duct tròn");
		Element element2 = doc.GetElement(refMain);
		MEPCurve mainPipe = element2 as MEPCurve;
		if (!ValidateSupportedPair(branchPipe, mainPipe, "Option 2", ref message)) return Result.Failed;
		if (((Element)branchPipe).Id == ((Element)mainPipe).Id)
		{
			TaskDialog.Show("Trim 3D", "Vui long chon 2 ong khac nhau.");
			return Result.Failed;
		}
		Line lineBranch = GetPipeLine(branchPipe);
		Line lineMain = GetPipeLine(mainPipe);
		if ((GeometryObject)(object)lineBranch == (GeometryObject)null || (GeometryObject)(object)lineMain == (GeometryObject)null)
		{
			TaskDialog.Show("Trim 3D", "Chi ho tro ong thang (straight pipe).");
			return Result.Failed;
		}
		XYZ bA = ((Curve)lineBranch).GetEndPoint(0);
		XYZ bB = ((Curve)lineBranch).GetEndPoint(1);
		XYZ mA = ((Curve)lineMain).GetEndPoint(0);
		XYZ mB = ((Curve)lineMain).GetEndPoint(1);
		XYZ dirBranch = lineBranch.Direction.Normalize();
		XYZ dirMain = lineMain.Direction.Normalize();
		double Z_branch = (bA.Z + bB.Z) / 2.0;
		double Z_main = (mA.Z + mB.Z) / 2.0;
		if (Math.Abs(Z_branch - Z_main) < 0.01)
		{
			TaskDialog.Show("Trim 3D – Option 2", "Ống nhánh và ống chính gần như cùng cao độ Z.\nOption 2 yêu cầu 2 ống có chênh lệch Z đáng kể.");
			return Result.Failed;
		}
		XYZ o1_proj = new XYZ(bA.X, bA.Y, Z_branch);
		XYZ o2_proj = new XYZ(mA.X, mA.Y, Z_branch);
		XYZ P_xy = Find2DIntersection(o1_proj, dirBranch, o2_proj, dirMain, Z_branch);
		if (P_xy == null)
		{
			TaskDialog.Show("Trim 3D – Option 2", "Hai ống song song, không tìm được điểm giao.\nOption 2 yêu cầu 2 ống cắt nhau khi nhìn từ trên xuống (XY).");
			return Result.Failed;
		}
		XYZ P_elbow_ideal = new XYZ(P_xy.X, P_xy.Y, Z_branch);
		XYZ P_elbow = ProjectOnLine(P_elbow_ideal, bA, dirBranch);
		XYZ P_tee_ideal = new XYZ(P_xy.X, P_xy.Y, Z_main);
		XYZ P_tee = ProjectOnLine(P_tee_ideal, mA, dirMain);
		bool pteeInside = IsPointBetweenEndpoints(P_tee, mA, mB, dirMain);
		TransactionGroup tg = new TransactionGroup(doc, "Trim 3D – Option 2");
		try
		{
			tg.Start();
			MEPCurve vertPipe = null;
			Transaction t1 = new Transaction(doc, "Opt2 T1 – Setup Pipes");
			try
			{
				t1.Start();
				XYZ endBranch = ((DistXY(bA, P_xy) <= DistXY(bB, P_xy)) ? bA : bB);
				if (!ExtendPipeEndTo(branchPipe, endBranch, P_elbow))
				{
					t1.RollBack();
					tg.RollBack();
					message = "Không thể trim/extend ống nhánh đến điểm elbow.";
					return Result.Failed;
				}
				doc.Regenerate();
				if (!pteeInside)
				{
					XYZ nearMain = ((mA.DistanceTo(P_tee) <= mB.DistanceTo(P_tee)) ? mA : mB);
					if (!ExtendPipeEndTo(mainPipe, nearMain, P_tee))
					{
						t1.RollBack();
						tg.RollBack();
						message = "Không thể extend ống chính đến điểm tee.";
						return Result.Failed;
					}
				}
				doc.Regenerate();
				try
				{
					vertPipe = CreateMatchingSegment(doc, mainPipe, branchPipe, P_elbow, P_tee);
					doc.Regenerate();
				}
				catch (Exception ex)
				{
					t1.RollBack();
					tg.RollBack();
					message = "Không tạo được ống đứng: " + ex.Message;
					return Result.Failed;
				}
				t1.Commit();
			}
			finally
			{
				((IDisposable)t1)?.Dispose();
			}
			MEPCurve mainPipeB = null;
			if (pteeInside && vertPipe != null)
			{
				Transaction t2 = new Transaction(doc, "Opt2 T2 – Split Main Pipe");
				try
				{
					t2.Start();
					try
					{
						ElementId newId = BreakCurve(doc, mainPipe, P_tee);
						Element element3 = doc.GetElement(newId);
						mainPipeB = element3 as MEPCurve;
					}
					catch
					{
					}
					t2.Commit();
				}
				finally
				{
					((IDisposable)t2)?.Dispose();
				}
			}
			Transaction t3 = new Transaction(doc, "Opt2 T3 – Tee at P_tee");
			try
			{
				t3.Start();
				if (vertPipe != null)
				{
					if (mainPipeB != null)
					{
						Connector cn1 = FindConnectorAtPoint(mainPipe, P_tee);
						Connector cn2 = FindConnectorAtPoint(mainPipeB, P_tee);
						Connector cn3 = FindConnectorAtPoint(vertPipe, P_tee);
						if (cn1 != null && cn2 != null && cn3 != null)
						{
							try
							{
								doc.Create.NewTeeFitting(cn1, cn2, cn3);
							}
							catch
							{
							}
						}
					}
					else
					{
						ConnectWithElbow(doc, mainPipe, vertPipe, P_tee);
					}
				}
				t3.Commit();
			}
			finally
			{
				((IDisposable)t3)?.Dispose();
			}
			Transaction t4 = new Transaction(doc, "Opt2 T4 – Elbow at P_elbow");
			try
			{
				t4.Start();
				if (vertPipe != null)
				{
					ConnectWithElbow(doc, branchPipe, vertPipe, P_elbow);
				}
				t4.Commit();
			}
			finally
			{
				((IDisposable)t4)?.Dispose();
			}
			tg.Assimilate();
		}
		finally
		{
			((IDisposable)tg)?.Dispose();
		}
		return Result.Succeeded;
	}

	private Result ExecuteOption3(UIDocument uidoc, Document doc, ref string message)
	{
		//IL_0417: Unknown result type (might be due to invalid IL or missing references)
		//IL_041f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0439: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_05d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_04cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_04d2: Expected O, but got Unknown
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_04d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_055e: Unknown result type (might be due to invalid IL or missing references)
		//IL_017d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0184: Expected O, but got Unknown
		//IL_0194: Unknown result type (might be due to invalid IL or missing references)
		//IL_019b: Expected O, but got Unknown
		//IL_015f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0166: Unknown result type (might be due to invalid IL or missing references)
		//IL_057a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0581: Expected O, but got Unknown
		//IL_01e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e8: Expected O, but got Unknown
		//IL_01c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_0584: Unknown result type (might be due to invalid IL or missing references)
		//IL_05a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0247: Unknown result type (might be due to invalid IL or missing references)
		//IL_024e: Expected O, but got Unknown
		//IL_05bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0288: Unknown result type (might be due to invalid IL or missing references)
		//IL_05d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c1: Expected O, but got Unknown
		//IL_02e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ec: Expected O, but got Unknown
		//IL_02ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0305: Expected O, but got Unknown
		//IL_0308: Unknown result type (might be due to invalid IL or missing references)
		//IL_0325: Unknown result type (might be due to invalid IL or missing references)
		//IL_032d: Unknown result type (might be due to invalid IL or missing references)
		//IL_033b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0442: Unknown result type (might be due to invalid IL or missing references)
		//IL_0388: Unknown result type (might be due to invalid IL or missing references)
		//IL_0390: Unknown result type (might be due to invalid IL or missing references)
		//IL_039e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0474: Unknown result type (might be due to invalid IL or missing references)
		//IL_047b: Expected O, but got Unknown
		//IL_047e: Unknown result type (might be due to invalid IL or missing references)
		Reference refBranch = uidoc.Selection.PickObject((ObjectType)1, (ISelectionFilter)(object)new Trim3DPipeFilter(), "Trim 3D Opt3 [1/2]: Chọn nhánh Pipe hoặc Duct tròn");
		Element element = doc.GetElement(refBranch);
		MEPCurve branchPipe = element as MEPCurve;
		Reference refMain = uidoc.Selection.PickObject((ObjectType)1, (ISelectionFilter)(object)new Trim3DPipeFilter(), "Trim 3D Opt3 [2/2]: Chọn chính Pipe hoặc Duct tròn");
		Element element2 = doc.GetElement(refMain);
		MEPCurve mainPipe = element2 as MEPCurve;
		if (!ValidateSupportedPair(branchPipe, mainPipe, "Option 3", ref message)) return Result.Failed;
		if (((Element)branchPipe).Id == ((Element)mainPipe).Id)
		{
			TaskDialog.Show("Trim 3D", "Vui long chon 2 ong khac nhau.");
			return Result.Failed;
		}
		Line lineBranch = GetPipeLine(branchPipe);
		Line lineMain = GetPipeLine(mainPipe);
		if ((GeometryObject)(object)lineBranch == (GeometryObject)null || (GeometryObject)(object)lineMain == (GeometryObject)null)
		{
			TaskDialog.Show("Trim 3D", "Chi ho tro ong thang (straight pipe).");
			return Result.Failed;
		}
		XYZ bA = ((Curve)lineBranch).GetEndPoint(0);
		XYZ bB = ((Curve)lineBranch).GetEndPoint(1);
		XYZ mA = ((Curve)lineMain).GetEndPoint(0);
		XYZ mB = ((Curve)lineMain).GetEndPoint(1);
		XYZ dirBranch = lineBranch.Direction.Normalize();
		XYZ dirMain = lineMain.Direction.Normalize();
		double Z_branch = (bA.Z + bB.Z) / 2.0;
		double Z_main = (mA.Z + mB.Z) / 2.0;
		if (Math.Abs(Z_branch - Z_main) < 0.01)
		{
			TaskDialog.Show("Trim 3D – Option 3", "Ống nhánh và ống chính gần như cùng cao độ Z.\nOption 3 yêu cầu 2 ống có chênh lệch Z đáng kể.");
			return Result.Failed;
		}
		XYZ o1_proj = new XYZ(bA.X, bA.Y, Z_branch);
		XYZ o2_proj = new XYZ(mA.X, mA.Y, Z_branch);
		XYZ P_xy = Find2DIntersection(o1_proj, dirBranch, o2_proj, dirMain, Z_branch);
		if (P_xy == null)
		{
			TaskDialog.Show("Trim 3D – Option 3", "Hai ống song song, không tìm được điểm giao.\nOption 3 yêu cầu 2 ống cắt nhau khi nhìn từ trên xuống (XY).");
			return Result.Failed;
		}
		XYZ P_tee_ideal = new XYZ(P_xy.X, P_xy.Y, Z_main);
		XYZ P_tee = ProjectOnLine(P_tee_ideal, mA, dirMain);
		XYZ endBranch = ((DistXY(bA, P_xy) <= DistXY(bB, P_xy)) ? bA : bB);
		double dZ = Math.Abs(Z_branch - Z_main);
		XYZ approachVec = new XYZ(P_xy.X - endBranch.X, P_xy.Y - endBranch.Y, 0.0);
		XYZ approachDir_flat = ((!(approachVec.GetLength() > 1E-06)) ? new XYZ(dirBranch.X, dirBranch.Y, 0.0).Normalize() : approachVec.Normalize());
		XYZ P_elbow_ideal = new XYZ(P_tee.X - approachDir_flat.X * dZ, P_tee.Y - approachDir_flat.Y * dZ, Z_branch);
		XYZ P_elbow = ProjectOnLine(P_elbow_ideal, bA, dirBranch);
		bool pteeInside = IsPointBetweenEndpoints(P_tee, mA, mB, dirMain);
		TransactionGroup tg = new TransactionGroup(doc, "Trim 3D – Option 3");
		try
		{
			tg.Start();
			MEPCurve diagPipe = null;
			Transaction t1 = new Transaction(doc, "Opt3 T1 – Setup Pipes");
			try
			{
				t1.Start();
				if (!ExtendPipeEndTo(branchPipe, endBranch, P_elbow))
				{
					t1.RollBack();
					tg.RollBack();
					message = "Không thể trim/extend ống nhánh đến điểm elbow 45°.";
					return Result.Failed;
				}
				doc.Regenerate();
				if (!pteeInside)
				{
					XYZ nearMain = ((mA.DistanceTo(P_tee) <= mB.DistanceTo(P_tee)) ? mA : mB);
					if (!ExtendPipeEndTo(mainPipe, nearMain, P_tee))
					{
						t1.RollBack();
						tg.RollBack();
						message = "Không thể extend ống chính đến điểm tee.";
						return Result.Failed;
					}
				}
				doc.Regenerate();
				try
				{
					diagPipe = CreateMatchingSegment(doc, mainPipe, branchPipe, P_elbow, P_tee);
					doc.Regenerate();
				}
				catch (Exception ex)
				{
					t1.RollBack();
					tg.RollBack();
					message = "Không tạo được ống xiên 45°: " + ex.Message;
					return Result.Failed;
				}
				t1.Commit();
			}
			finally
			{
				((IDisposable)t1)?.Dispose();
			}
			MEPCurve mainPipeB = null;
			if (pteeInside && diagPipe != null)
			{
				Transaction t2 = new Transaction(doc, "Opt3 T2 – Split Main Pipe");
				try
				{
					t2.Start();
					try
					{
						ElementId newId = BreakCurve(doc, mainPipe, P_tee);
						Element element3 = doc.GetElement(newId);
						mainPipeB = element3 as MEPCurve;
					}
					catch
					{
					}
					t2.Commit();
				}
				finally
				{
					((IDisposable)t2)?.Dispose();
				}
			}
			Transaction t3 = new Transaction(doc, "Opt3 T3 – Tee at P_tee");
			try
			{
				t3.Start();
				if (diagPipe != null)
				{
					if (mainPipeB != null)
					{
						Connector cn1 = FindConnectorAtPoint(mainPipe, P_tee);
						Connector cn2 = FindConnectorAtPoint(mainPipeB, P_tee);
						Connector cn3 = FindConnectorAtPoint(diagPipe, P_tee);
						if (cn1 != null && cn2 != null && cn3 != null)
						{
							try
							{
								doc.Create.NewTeeFitting(cn1, cn2, cn3);
							}
							catch
							{
							}
						}
					}
					else
					{
						ConnectWithElbow(doc, mainPipe, diagPipe, P_tee);
					}
				}
				t3.Commit();
			}
			finally
			{
				((IDisposable)t3)?.Dispose();
			}
			Transaction t4 = new Transaction(doc, "Opt3 T4 – Elbow 45° at P_elbow");
			try
			{
				t4.Start();
				if (diagPipe != null)
				{
					ConnectWithElbow(doc, branchPipe, diagPipe, P_elbow);
				}
				t4.Commit();
			}
			finally
			{
				((IDisposable)t4)?.Dispose();
			}
			tg.Assimilate();
		}
		finally
		{
			((IDisposable)tg)?.Dispose();
		}
		return Result.Succeeded;
	}

	private XYZ Find2DIntersection(XYZ pA, XYZ dA, XYZ pB, XYZ dB, double zPlane)
	{
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Expected O, but got Unknown
		double cross = dA.X * dB.Y - dA.Y * dB.X;
		if (Math.Abs(cross) < 1E-06)
		{
			return null;
		}
		double dx = pB.X - pA.X;
		double dy = pB.Y - pA.Y;
		double t = (dx * dB.Y - dy * dB.X) / cross;
		return new XYZ(pA.X + t * dA.X, pA.Y + t * dA.Y, zPlane);
	}

	private double DistXY(XYZ a, XYZ b)
	{
		return Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
	}

	private XYZ ProjectOnLine(XYZ pt, XYZ origin, XYZ dir)
	{
		XYZ d = dir.Normalize();
		return origin + d * (pt - origin).DotProduct(d);
	}

	private Line GetPipeLine(MEPCurve pipe)
	{
		Location location = ((Element)pipe).Location;
		Location obj = ((location is LocationCurve) ? location : null);
		Curve obj2 = ((obj != null) ? ((LocationCurve)obj).Curve : null);
		return (Line)(object)((obj2 is Line) ? obj2 : null);
	}

	private bool IsPointBetweenEndpoints(XYZ pt, XYZ pA, XYZ pB, XYZ dir)
	{
		XYZ d = dir.Normalize();
		double tEnd = (pB - pA).DotProduct(d);
		double tPt = (pt - pA).DotProduct(d);
		if (tEnd < 0.0)
		{
			tPt = 0.0 - tPt;
			tEnd = 0.0 - tEnd;
		}
		return tPt > 0.003 && tPt < tEnd - 0.003;
	}

	private bool ExtendPipeEndTo(MEPCurve pipe, XYZ pipeEnd, XYZ newPt)
	{
		if (pipeEnd.DistanceTo(newPt) < 0.001)
		{
			return true;
		}
		Location location = ((Element)pipe).Location;
		LocationCurve lc = (LocationCurve)(object)((location is LocationCurve) ? location : null);
		Curve obj = ((lc != null) ? lc.Curve : null);
		Line line = (Line)(object)((obj is Line) ? obj : null);
		if ((GeometryObject)(object)line == (GeometryObject)null)
		{
			return false;
		}
		XYZ p0 = ((Curve)line).GetEndPoint(0);
		XYZ p1 = ((Curve)line).GetEndPoint(1);
		XYZ fixedEnd = ((p0.DistanceTo(pipeEnd) < p1.DistanceTo(pipeEnd)) ? p1 : p0);
		XYZ origDir = (p1 - p0).Normalize();
		Line newLine = (((newPt - fixedEnd).DotProduct(origDir) >= 0.0) ? Line.CreateBound(fixedEnd, newPt) : Line.CreateBound(newPt, fixedEnd));
		if (((Curve)newLine).Length < 0.003)
		{
			return false;
		}
		try
		{
			lc.Curve = (Curve)(object)newLine;
			return true;
		}
		catch
		{
			return false;
		}
	}

	private void ConnectWithElbow(Document doc, MEPCurve pA, MEPCurve pB, XYZ junction)
	{
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Expected O, but got Unknown
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		Connector cA = GetFreeConn(pA, junction);
		Connector cB = GetFreeConn(pB, junction);
		if (cA == null || cB == null)
		{
			return;
		}
		SubTransaction st = new SubTransaction(doc);
		try
		{
			st.Start();
			try
			{
				doc.Create.NewElbowFitting(cA, cB);
			}
			catch
			{
				try
				{
					cA.ConnectTo(cB);
				}
				catch
				{
				}
			}
			st.Commit();
		}
		finally
		{
			((IDisposable)st)?.Dispose();
		}
	}

	private Connector FindConnectorAtPoint(MEPCurve pipe, XYZ point)
	{
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Expected O, but got Unknown
		object obj;
		if (pipe == null)
		{
			obj = null;
		}
		else
		{
			ConnectorManager connectorManager = ((MEPCurve)pipe).ConnectorManager;
			obj = ((connectorManager != null) ? connectorManager.Connectors : null);
		}
		if (obj == null)
		{
			return null;
		}
		foreach (Connector connector in ((MEPCurve)pipe).ConnectorManager.Connectors)
		{
			Connector cn = connector;
			if (Math.Round(cn.Origin.DistanceTo(point) * 304.8, 3) == 0.0)
			{
				return cn;
			}
		}
		return null;
	}

	private Connector GetFreeConn(MEPCurve pipe, XYZ near)
	{
		return (from Connector c in (IEnumerable)((MEPCurve)pipe).ConnectorManager.Connectors
			where !c.IsConnected && (int)c.Domain != 5
			orderby c.Origin.DistanceTo(near)
			select c).FirstOrDefault();
	}

	private PipingSystemType GetPipeSystem(Document doc, Pipe pipe)
	{
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Expected I4, but got Unknown
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Expected O, but got Unknown
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Invalid comparison between Unknown and I4
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Expected O, but got Unknown
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Invalid comparison between Unknown and I4
		foreach (Connector connector in ((MEPCurve)pipe).ConnectorManager.Connectors)
		{
			Connector c = connector;
			if ((int)c.Domain != 3)
			{
				continue;
			}
			try
			{
				MEPSystem mEPSystem = c.MEPSystem;
				PipingSystem ps = (PipingSystem)(object)((mEPSystem is PipingSystem) ? mEPSystem : null);
				if (ps != null)
				{
					Element element = doc.GetElement(((Element)ps).GetTypeId());
					PipingSystemType t = (PipingSystemType)(object)((element is PipingSystemType) ? element : null);
					if (t != null)
					{
						return t;
					}
				}
			}
			catch
			{
			}
			int sysInt = (int)c.PipeSystemType;
			foreach (PipingSystemType item in new FilteredElementCollector(doc).OfClass(typeof(PipingSystemType)))
			{
				PipingSystemType st = item;
				if ((int)((MEPSystemType)st).SystemClassification == sysInt)
				{
					return st;
				}
			}
		}
		return ((IEnumerable)new FilteredElementCollector(doc).OfClass(typeof(PipingSystemType))).Cast<PipingSystemType>().FirstOrDefault();
	}

	// Trim 3D has three geometric paths, but the generated segment must remain in
	// the same MEP domain as the selected curves.  Round ducts are intentionally
	// supported first; rectangular ducts need an orientation-aware implementation.
	private bool ValidateSupportedPair(MEPCurve first, MEPCurve second, string optionName, ref string message)
	{
		if (first == null || second == null)
		{
			message = "Chỉ chọn Pipe hoặc Duct.";
			TaskDialog.Show("Trim 3D", message);
			return false;
		}
		if (first.Id == second.Id)
		{
			message = "Vui lòng chọn hai ống khác nhau.";
			TaskDialog.Show("Trim 3D", message);
			return false;
		}
		if (first is Pipe && second is Pipe) return true;
		if (first is Duct firstDuct && second is Duct secondDuct && IsRoundDuct(firstDuct) && IsRoundDuct(secondDuct)) return true;

		message = optionName + " chỉ nối hai Pipe hoặc hai Duct tròn cùng loại. Duct chữ nhật chưa được hỗ trợ trong Trim 3D.";
		TaskDialog.Show("Trim 3D", message);
		return false;
	}

	private bool IsRoundDuct(Duct duct)
	{
		// Do not read MEPCurve.Diameter until the profile has been established.
		// Revit throws "The shape of the connector is not Round." for a
		// rectangular duct when that property is accessed.
		if (duct == null) return false;
		Connector[] ends = duct.ConnectorManager.Connectors.Cast<Connector>()
			.Where(c => c.ConnectorType == ConnectorType.End && c.Domain == Domain.DomainHvac).ToArray();
		return ends.Length == 2 && ends.All(c => c.Shape == ConnectorProfileType.Round);
	}

	private MEPCurve CreateMatchingSegment(Document doc, MEPCurve typeSource, MEPCurve sizeSource, XYZ start, XYZ end)
	{
		ElementId levelId = GetNearestLevel(doc, Math.Min(start.Z, end.Z));
		if (typeSource is Pipe typePipe && sizeSource is Pipe sizePipe)
		{
			PipingSystemType pipeSystem = GetPipeSystem(doc, typePipe);
			Pipe newPipe = Pipe.Create(doc, pipeSystem.Id, typePipe.PipeType.Id, levelId, start, end);
			newPipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(((MEPCurve)sizePipe).Diameter);
			return newPipe;
		}
		if (typeSource is Duct typeDuct && sizeSource is Duct sizeDuct && IsRoundDuct(typeDuct) && IsRoundDuct(sizeDuct))
		{
			ElementId systemTypeId = typeDuct.get_Parameter((BuiltInParameter)(-1140333))?.AsElementId();
			if (systemTypeId == null || systemTypeId == ElementId.InvalidElementId)
			{
				foreach (Connector connector in typeDuct.ConnectorManager.Connectors)
				{
					if (connector.MEPSystem != null)
					{
						systemTypeId = connector.MEPSystem.GetTypeId();
						break;
					}
				}
			}
			if (systemTypeId == null || systemTypeId == ElementId.InvalidElementId)
				throw new System.InvalidOperationException("Không tìm thấy Duct System Type của Duct đã chọn.");

			Duct newDuct = Duct.Create(doc, systemTypeId, typeDuct.DuctType.Id, levelId, start, end);
			newDuct.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM)?.Set(((MEPCurve)sizeDuct).Diameter);
			return newDuct;
		}
		throw new System.InvalidOperationException("Hai đối tượng phải cùng là Pipe hoặc cùng là Duct tròn.");
	}

	private ElementId BreakCurve(Document doc, MEPCurve curve, XYZ point)
	{
		if (curve is Pipe) return PlumbingUtils.BreakCurve(doc, curve.Id, point);
		if (curve is Duct) return MechanicalUtils.BreakCurve(doc, curve.Id, point);
		throw new System.InvalidOperationException("Trim 3D chỉ hỗ trợ Pipe và Duct tròn.");
	}

	private ElementId GetNearestLevel(Document doc, double z)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		Level lv = (from Level l in (IEnumerable)new FilteredElementCollector(doc).OfClass(typeof(Level))
			orderby Math.Abs(l.Elevation - z)
			select l).FirstOrDefault();
		return ((lv != null) ? ((Element)lv).Id : null) ?? ElementId.InvalidElementId;
	}
}
