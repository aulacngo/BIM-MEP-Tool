using RibbonPanel = Autodesk.Revit.UI.RibbonPanel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Autodesk.Windows;

namespace BIN;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class Panel : IExternalApplication
{
	private readonly string nameSpace = "BIN.";

	private readonly string tabMep = "BIM - MEP";
	private readonly string tabDocs = "BIM - DOCS";

	private readonly string path = Assembly.GetExecutingAssembly().Location;

	private BitmapImage LoadImage(string imgName)
	{
		string assemblyDir = Path.GetDirectoryName(path);
		string imgPath = Path.Combine(assemblyDir, "Resources", imgName + ".png");
		if (!File.Exists(imgPath))
		{
			return null;
		}
		try
		{
			BitmapImage bitmapImage = new BitmapImage();
			bitmapImage.BeginInit();
			bitmapImage.UriSource = new Uri(imgPath, UriKind.Absolute);
			bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage.EndInit();
			return bitmapImage;
		}
		catch
		{
			return null;
		}
	}

	private BitmapImage LoadSmallImage(string imgName)
	{
		string assemblyDir = Path.GetDirectoryName(path);
		string smallPath = Path.Combine(assemblyDir, "Resources", imgName + "_small.png");
		string regularPath = Path.Combine(assemblyDir, "Resources", imgName + ".png");
		string imgPath = (File.Exists(smallPath) ? smallPath : regularPath);
		if (!File.Exists(imgPath))
		{
			return null;
		}
		try
		{
			BitmapImage bitmapImage = new BitmapImage();
			bitmapImage.BeginInit();
			bitmapImage.UriSource = new Uri(imgPath, UriKind.Absolute);
			bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage.EndInit();
			return bitmapImage;
		}
		catch
		{
			return null;
		}
	}

	private BitmapImage LoadToolTipImage(string imgName)
	{
		string assemblyDir = Path.GetDirectoryName(path);
		string imgPath = Path.Combine(assemblyDir, "Resources", imgName + "_tooltip.png");
		if (!File.Exists(imgPath))
		{
			return null;
		}
		try
		{
			BitmapImage bitmapImage = new BitmapImage();
			bitmapImage.BeginInit();
			bitmapImage.UriSource = new Uri(imgPath, UriKind.Absolute);
			bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage.EndInit();
			return bitmapImage;
		}
		catch
		{
			return null;
		}
	}

	private PushButtonData CreatePushData(string name, string text, string className, string imgName, string tooltip)
	{
		DevCommandRegistry.Register(name, className);
		return new PushButtonData(name, text, path, nameSpace + "DevProxy_" + name)
		{
			ToolTip = tooltip,
			LargeImage = LoadImage(imgName),
			Image = LoadSmallImage(imgName),
			ToolTipImage = LoadToolTipImage(imgName)
		};
	}

	private void AddLargeButtons(RibbonPanel panel, List<PushButtonData> buttons)
	{
		foreach (PushButtonData btn in buttons)
		{
			panel.AddItem((RibbonItemData)(object)btn);
		}
	}

	// ==========================================
	// TAB 1: BIM - MEP PANELS
	// ==========================================

	private void Micro_Tool(RibbonPanel panel)
	{
		// 1. Move Connect & Disconnect (SplitButton)
		SplitButtonData sbConnectData = new SplitButtonData("Split_Connect", "Move\nConnect");
		if (panel.AddItem(sbConnectData) is SplitButton sbConnect)
		{
			sbConnect.AddPushButton(CreatePushData("MoveConnect", "Move\nConnect", "MoveConnectCmd", "MoveConnect", "Move Connect - Di chuyen va tu dong noi ong (Auto-connect)"));
			sbConnect.AddPushButton(CreatePushData("Disconnect", "Disconnect", "DisconnectCmd", "Disconnect", "Disconnect - Ngat ket noi fitting va ong"));
		}

		// 2. Trim 3D (Connect 2 pipes at different elevations)
		panel.AddItem(CreatePushData("Trim3D", "Trim\n3D", "Trim3DCmd", "Trim3D", "Trim 3D - Noi 2 ong khac cao do Z bang 2 elbow hoac elbow + tee"));

		// 3. Elbow 90 deg
		SplitButtonData sbElbow90Data = new SplitButtonData("Split_Elbow90", "Elbow\n90");
		if (panel.AddItem(sbElbow90Data) is SplitButton sbElbow90)
		{
			sbElbow90.AddPushButton(CreatePushData("ElbowUp", "Elbow\nUp", "ElbowUpCmd", "ElbowUp", "Elbow Up 90 deg"));
			sbElbow90.AddPushButton(CreatePushData("ElbowDown", "Elbow\nDown", "ElbowDownCmd", "ElbowDown", "Elbow Down 90 deg"));
			sbElbow90.AddPushButton(CreatePushData("ElbowLeft", "Elbow\nLeft", "ElbowLeftCmd", "ElbowLeft", "Elbow Left 90 deg"));
			sbElbow90.AddPushButton(CreatePushData("ElbowRight", "Elbow\nRight", "ElbowRightCmd", "ElbowRight", "Elbow Right 90 deg"));
		}

		// 4. Elbow 45 deg
		SplitButtonData sbElbow45Data = new SplitButtonData("Split_Elbow45", "Elbow\n45");
		if (panel.AddItem(sbElbow45Data) is SplitButton sbElbow45)
		{
			sbElbow45.AddPushButton(CreatePushData("ElbowUp45", "Elbow\nUp 45", "ElbowUp45Cmd", "ElbowUp45", "Elbow Up 45 deg"));
			sbElbow45.AddPushButton(CreatePushData("ElbowDown45", "Elbow\nDown 45", "ElbowDown45Cmd", "ElbowDown", "Elbow Down 45 deg"));
			sbElbow45.AddPushButton(CreatePushData("ElbowLeft45", "Elbow\nLeft 45", "ElbowLeft45Cmd", "ElbowLeft", "Elbow Left 45 deg"));
			sbElbow45.AddPushButton(CreatePushData("ElbowRight45", "Elbow\nRight 45", "ElbowRight45Cmd", "ElbowRight", "Elbow Right 45 deg"));
		}

		// 5. Rotate (Single)
		panel.AddItem(CreatePushData("RotateElements", "Rotate", "RotateElementsCmd", "RotateElements", "Rotate - Xoay cut theo truc chi dinh (Chon cut -> Chon truc -> Go SPACE)"));

		// 6. Rotate Multi (Batch)
		panel.AddItem(CreatePushData("RotateMulti", "Rotate\nMulti", "RotateMultiCmd", "RotateMulti", "Rotate Multi - Xoay dong loat nhieu cut tren cac tuyen ong song song"));

		// 7. Quick Alignment (SplitButton: 3D Align, Branch Align Lite)
		SplitButtonData sbAlignData = new SplitButtonData("Split_Align", "3D\nAlign");
		if (panel.AddItem(sbAlignData) is SplitButton sbAlign)
		{
			sbAlign.AddPushButton(CreatePushData("ThreeDAlign", "3D\nAlign", "ThreeDAlignCmd", "AlignIn3D", "3D Align - Can chinh tim ong trong khong gian 3D"));
			sbAlign.AddPushButton(CreatePushData("BranchAlignLite", "Branch\nLite", "BranchAlignLiteCmd", "BranchAlignLite", "Branch Align - Can chinh nhanh ong phu"));
			sbAlign.AddPushButton(CreatePushData("AlignPipeElevation", "Align\nElev", "AlignPipeElevationCmd", "AlignPipeElevation", "Align Pipe Elevation - Can bang cao do ong ngang (Middle Elevation)"));
		}

		// 8. System Cleanup (Pulldown: Delete System, Delete Orphan Insu)
		PulldownButtonData pdCleanData = new PulldownButtonData("Pulldown_Clean", "Delete\nTools")
		{
			ToolTip = "Cong cu don dep he thong va bao on mo coi",
			LargeImage = LoadImage("DeleteSystem"),
			Image = LoadSmallImage("DeleteSystem"),
			ToolTipImage = LoadToolTipImage("DeleteSystem")
		};
		if (panel.AddItem(pdCleanData) is PulldownButton pdClean)
		{
			pdClean.AddPushButton(CreatePushData("DeleteSystem", "Delete\nSystem", "DeleteSystemCmd", "DeleteSystem", "Delete System - Xoa toan bo mang luoi he thong noi lien"));
			pdClean.AddPushButton(CreatePushData("DeleteOrphanInsu", "Delete\nOrphan Insu", "DeleteOrphanInsuCmd", "DeleteOrphanInsu", "Delete Orphan Insu - Xoa sach lop bao on mo coi"));
		}
	}

	private void MEP_Tool(RibbonPanel panel)
	{
		List<PushButtonData> p = new List<PushButtonData>
		{
			CreatePushData("AvoidClash", "Avoid\nClash", "AvoidClashCmd", "AvoidClash", "Avoid Clash - Tu dong uon 4 cut ne va cham"),
			CreatePushData("Bloom", "Bloom", "BloomCmd", "CreatePipeFromFitting", "Bloom"),
			CreatePushData("SplitDuct", "Split\nDuct", "SplitDuctCmd", "SplitDuct", "Split Duct"),
			CreatePushData("PlaceFamily", "Place\nFamily", "PlaceFamily", "PlaceFamily", "Place Family"),
			CreatePushData("RotateFamilyInstance", "Rotate\nFamily", "RotateFamilyInstance", "RotateFamilyInstance", "Rotate Family Instance"),
			CreatePushData("ChangeReferenceLevel", "Change\nRef Level", "ChangeReferenceLevel", "ChangeReferenceLevel", "Change Reference Level"),
			CreatePushData("ToggleInsulation", "Toggle\nInsulation", "ToggleInsulation", "ToggleInsulation", "Toggle Insulation"),
			CreatePushData("TurnMEP", "Turn\nMEP", "TurnMEP", "TurnMEP", "Turn MEP"),
			CreatePushData("MEPOffset", "MEP\nOffset", "MEPOffset", "MEPOffset", "MEP Offset"),
			CreatePushData("CutMEPByLevel", "Cut MEP\nBy Level", "CutMEPByLevel", "CutMEPByLevel", "Cut MEP By Level"),
			CreatePushData("PipeInsulation", "Pipe\nInsulation", "PipeInsulationCmd", "PipeInsulation", "Pipe Insulation"),
			CreatePushData("DrawMultiPipe", "Draw\nMulti Pipe", "DrawMultiPipe", "DrawMultiPipe", "Draw Multi Pipe"),
			CreatePushData("ManageElement", "Manage\nElement", "SelectElement", "SelectElement", "Manage Element"),
			CreatePushData("SelectByParameter", "Select By\nParameter", "SelectByParameterCommand", "SelectByParameter", "Select By Parameter"),
			CreatePushData("CompareElement", "Compare\nElement", "CompareElementCommand", "CompareElement", "Compare Element"),
			CreatePushData("ClashDetective", "Clash\nDetective", "ClashDetectiveCmd", "ClashDetective", "Clash Detective")
		};
		AddLargeButtons(panel, p);
	}

	private void Sprinkler_Tool(RibbonPanel panel)
	{
		List<PushButtonData> p = new List<PushButtonData>
		{
			CreatePushData("OptimizePipeSegments", "Optimize\nPipes", "OptimizePipeSegments", "OptimizePipeSegments", "Optimize Pipe Segments"),
			CreatePushData("ReplacePipeWithNipple", "Replace Pipe\nWith Nipple", "ReplacePipeWithNipple", "ReplacePipeWithNipple", "Replace Pipe With Nipple"),
			CreatePushData("ShortenSprinkler", "Shorten\nSprinkler", "SprinklerShortenerVerticalPipe", "SprinklerShortenerVerticalPipe", "Sprinkler Shortener Vertical Pipe"),
			CreatePushData("ToReducingElbow", "Reducing\nElbow", "TransferToReducingElbow", "TransferToReducingElbow", "Transfer To Reducing Elbow"),
			CreatePushData("SprinklerFlipper", "Sprinkler\nFlipper", "SprinklerFlipper", "SprinklerFlipper", "Sprinkler Flipper Up To Down"),
			CreatePushData("RemovePipeCoupling", "Remove\nCoupling", "RemovePipeCoupling", "RemovePipeCoupling", "Remove Pipe Coupling"),
			CreatePushData("ConnectSprinklerToPipe", "Sprinkler\nTo Pipe", "ConnectSprinklerToPipe", "ConnectSprinklerToPipe", "Connect Sprinkler To Pipe"),
			CreatePushData("ConnectSprinklerFlexPipe", "Sprinkler\nFlex Pipe", "ConnectSprinklerFlexPipeCmd", "ConnectSprinklerFlexPipe", "Connect Sprinkler to Pipe using Flexible Pipe and Reducer"),
			CreatePushData("ConnectSprinklerFlexMulti", "Sprinkler\nFlex Multi", "ConnectSprinklerFlexPipeMultiCmd", "ConnectSprinklerFlexMulti", "Preselect multiple open-end Pipes and Sprinklers, then connect all pairs using Elbows"),
			CreatePushData("ConnectTeeAndReducer_Groove", "Tee/Reducer\nGroove", "ConnectTeeAndReducer_Groove", "ConnectTeeAndReducer_Groove", "Connect Tee And Reducer - Groove"),
			CreatePushData("CheckSprinkler", "Check\nSprinkler", "CheckSprinklerCmd", "CheckSprinkler", "Check Sprinkler")
		};
		AddLargeButtons(panel, p);
	}

	private void Drainage_Tool(RibbonPanel panel)
	{
		List<PushButtonData> p = new List<PushButtonData>
		{
			CreatePushData("ConnectVentRiser", "Connect\nVent Riser", "ConnectVentRiser", "ConnectVentRiser", "Connect Vent Riser"),
			CreatePushData("Create2Elbows45", "Create Elbows\n45 deg", "Create2Elbows45", "Create2Elbows45", "Create 2 Elbows 45"),
			CreatePushData("Join2Elbows", "Join\n2 Elbows", "Join2Elbows", "Join2Elbows", "Join 2 Elbows"),
			CreatePushData("TurnDrainagePipe", "Turn\nDrainage", "TurnDrainagePipe", "TurnDrainagePipe", "Turn Drainage Pipe"),
			CreatePushData("DrawDrainagePipeType1", "Draw\nDrainage", "DrawDrainagePipe_AllTypes", "DrawDrainagePipeType1", "Draw Drainage Pipe"),
			CreatePushData("DrawCleanOut", "Draw\nClean Out", "DrawCleanOut", "DrawCleanOut", "Draw Clean Out")
		};
		AddLargeButtons(panel, p);
	}

	private void Support_Tool(RibbonPanel panel)
	{
		List<PushButtonData> p = new List<PushButtonData>
		{
			CreatePushData("PlaceMultiPipeSupport", "Multi Pipe\nSupport", "PlaceMultiPipeSupport", "PlaceMultiPipeSupport", "Place Multi Pipe Support"),
			CreatePushData("PlaceSupport", "Place\nSupport", "PlaceSupport", "PlaceSupport", "Place Support"),
			CreatePushData("AlignSupport", "Align\nSupport", "AlignSupportCmd", "AlignSupport", "Align Support - Can chinh support ve tam ong gan nhat"),
			CreatePushData("MoveSupport", "Move\nSupport", "MoveSupportCmd", "MoveSupport", "Move Support - Di doi support ve 1 diem tren pipe, duct, cable tray")
		};
		AddLargeButtons(panel, p);
	}

	private void Check_Tool(RibbonPanel panel)
	{
		List<PushButtonData> p = new List<PushButtonData>
		{
			CreatePushData("CheckAirTerminal", "Check\nAir Terminal", "CheckAirTerminalCmd", "CheckAirTerminal", "Check Air Terminal"),
			CreatePushData("CheckPipeFitting", "Check\nPipe Fitting", "CheckPipeFittingCmd", "CheckPipeFitting", "Check Pipe Fitting"),
			CreatePushData("CheckConnectedPipe", "Check\nConnected Pipe", "CheckConnectedPipeCmd", "CheckConnectedPipe", "Check Connected Pipe"),
			CreatePushData("CheckUndefinedPipe", "Check\nUndefined Pipe", "CheckUndefinedPipeCmd", "CheckUndefinedPipe", "Check Undefined Pipe"),
			CreatePushData("FlexDuctAvoidMep", "Flex Duct\nAvoid MEP", "FlexDuctAvoidMepCmd", "FlexDuctAvoidMep", "Flex Duct Avoid MEP"),
			CreatePushData("GetId", "Get\nID", "GetIdCmd", "GetId", "Get Element ID (ho tro file link)")
			,CreatePushData("CheckPipeClash", "Pipe Clash\nClearance", "CheckPipeClashCmd", "CheckPipeClash", "Local 2-3 Pipe Clash and insulation clearance check")
		};
		AddLargeButtons(panel, p);
	}

	// ==========================================
	// TAB 2: BIM - DOCS PANELS
	// ==========================================

	private void BOQExcel_Tool(RibbonPanel panel)
	{
		List<PushButtonData> p = new List<PushButtonData>
		{
			CreatePushData("BOQ", "BOQ", "BOQCommand", "BOQ", "Bill of Quantities"),
			CreatePushData("ExportScheduleToExcel", "Schedule To\nExcel", "ExportScheduleToExcel", "ExportScheduleToExcel", "Export Schedule To Excel")
		};
		AddLargeButtons(panel, p);
	}

	private void SheetView_Tool(RibbonPanel panel)
	{
		List<PushButtonData> p = new List<PushButtonData>
		{
			CreatePushData("SheetFromExcel", "Sheet\nFrom Excel", "SheetFromExcel", "SheetFromExcel", "Sheet From Excel"),
			CreatePushData("CopySheet", "Copy\nSheet", "CopySheet", "CopySheet", "Copy Sheet"),
			CreatePushData("CopyView", "Copy\nView", "CopyView", "CopyView", "Copy View"),
			CreatePushData("CopyFilter", "Copy\nFilter", "CopyFilterCmd", "CopyFilter", "Copy view filters to other views")
		};
		AddLargeButtons(panel, p);
	}

	private void Annotation2D_Tool(RibbonPanel panel)
	{
		List<PushButtonData> p = new List<PushButtonData>
		{
			CreatePushData("ManageGridLevel", "Grid &\nLevel", "ManageGridLevel", "ManageGridLevel", "Manage Grid & Level"),
			CreatePushData("ToggleSectionHead", "Section\nHead/Tail", "ToggleSectionHeadCmd", "ToggleSectionHead", "Toggle Section Head and Tail"),
			CreatePushData("Delete2DElement", "Delete 2D\nElement", "Delete2DElement", "Delete2DElement", "Delete 2D Element"),
			CreatePushData("Rename2DElement", "Rename 2D\nElement", "Rename2DElement", "Rename2DElement", "Rename 2D Element")
		};
		AddLargeButtons(panel, p);
	}

	private void TransferLink_Tool(RibbonPanel panel)
	{
		List<PushButtonData> p = new List<PushButtonData>
		{
			CreatePushData("ExportFamily", "Export\nFamily", "ExportFamily", "ExportFamily", "Export Family"),
			CreatePushData("TransferDocument", "Transfer\nDocument", "TransferDocument", "TransferDocument", "Transfer Document"),
			CreatePushData("CopyFromLink", "Copy From\nLink", "CopyFromLinkCmd", "CopyFromLink", "Copy From Link File")
		};
		AddLargeButtons(panel, p);
	}

	// ==========================================
	// STARTUP & SHUTDOWN
	// ==========================================

	public Result OnStartup(UIControlledApplication application)
	{
		try
		{
			DocumentTelemetryTracker.Start();
			application.ControlledApplication.DocumentChanged -= DocumentTelemetryTracker.OnDocumentChanged;
			application.ControlledApplication.DocumentChanged += DocumentTelemetryTracker.OnDocumentChanged;
		}
		catch { DocumentTelemetryTracker.Stop(); }

		try
		{
			// 1. Tao 2 Tab rieng biet: BIM - MEP va BIM - DOCS
			try { application.CreateRibbonTab(tabMep); } catch { }
			try { application.CreateRibbonTab(tabDocs); } catch { }

			// --- TAB 1: BIM - MEP PANELS ---
			Autodesk.Revit.UI.RibbonPanel micro_Panel = null;
			Autodesk.Revit.UI.RibbonPanel mep_Panel = null;
			Autodesk.Revit.UI.RibbonPanel sprinkler_Panel = null;
			Autodesk.Revit.UI.RibbonPanel drainage_Panel = null;
			Autodesk.Revit.UI.RibbonPanel support_Panel = null;
			Autodesk.Revit.UI.RibbonPanel check_Panel = null;

			try { micro_Panel = application.CreateRibbonPanel(tabMep, "BIM - MICRO"); } catch { }
			try { mep_Panel = application.CreateRibbonPanel(tabMep, "BIM - MEP"); } catch { }
			try { sprinkler_Panel = application.CreateRibbonPanel(tabMep, "BIM - SPRINKLER"); } catch { }
			try { drainage_Panel = application.CreateRibbonPanel(tabMep, "BIM - DRAINAGE"); } catch { }
			try { support_Panel = application.CreateRibbonPanel(tabMep, "BIM - SUPPORT"); } catch { }
			try { check_Panel = application.CreateRibbonPanel(tabMep, "BIM - CHECK"); } catch { }

			if (micro_Panel != null) { try { Micro_Tool(micro_Panel); } catch { } }
			if (mep_Panel != null) { try { MEP_Tool(mep_Panel); } catch { } }
			if (sprinkler_Panel != null) { try { Sprinkler_Tool(sprinkler_Panel); } catch { } }
			if (drainage_Panel != null) { try { Drainage_Tool(drainage_Panel); } catch { } }
			if (support_Panel != null) { try { Support_Tool(support_Panel); } catch { } }
			if (check_Panel != null) { try { Check_Tool(check_Panel); } catch { } }

			// --- TAB 2: BIM - DOCS PANELS ---
			Autodesk.Revit.UI.RibbonPanel boq_Panel = null;
			Autodesk.Revit.UI.RibbonPanel sheet_Panel = null;
			Autodesk.Revit.UI.RibbonPanel anno_Panel = null;
			Autodesk.Revit.UI.RibbonPanel transfer_Panel = null;

			try { boq_Panel = application.CreateRibbonPanel(tabDocs, "BIM - BOQ & EXCEL"); } catch { }
			try { sheet_Panel = application.CreateRibbonPanel(tabDocs, "BIM - SHEET & VIEW"); } catch { }
			try { anno_Panel = application.CreateRibbonPanel(tabDocs, "BIM - 2D ANNOTATION"); } catch { }
			try { transfer_Panel = application.CreateRibbonPanel(tabDocs, "BIM - TRANSFER & LINK"); } catch { }

			if (boq_Panel != null) { try { BOQExcel_Tool(boq_Panel); } catch { } }
			if (sheet_Panel != null) { try { SheetView_Tool(sheet_Panel); } catch { } }
			if (anno_Panel != null) { try { Annotation2D_Tool(anno_Panel); } catch { } }
			if (transfer_Panel != null) { try { TransferLink_Tool(transfer_Panel); } catch { } }

			try
			{
				application.Idling += (sender, args) =>
				{
					if (sender is UIApplication uiApp)
					{
						RevitMcpServer.Start(uiApp);
					}
				};
			}
			catch { }

			try
			{
				if (ComponentManager.Ribbon != null && ComponentManager.Ribbon.Tabs != null)
				{
					foreach (Autodesk.Windows.RibbonTab tab in ComponentManager.Ribbon.Tabs)
					{
						if (tab.Id != tabMep && tab.Id != tabDocs)
						{
							continue;
						}
						if (tab.Panels == null) continue;
						foreach (Autodesk.Windows.RibbonPanel panel in tab.Panels)
						{
							try
							{
								if (panel.Source != null)
								{
									string title = panel.Source.Title;
									if (title == "BIM - MICRO")
										panel.CustomPanelTitleBarBackground = new SolidColorBrush(Color.FromRgb(255, 200, 255));
									else if (title == "BIM - MEP")
										panel.CustomPanelTitleBarBackground = new SolidColorBrush(Color.FromRgb(200, 255, 200));
									else if (title == "BIM - SPRINKLER")
										panel.CustomPanelTitleBarBackground = new SolidColorBrush(Color.FromRgb(255, 200, 200));
									else if (title == "BIM - DRAINAGE")
										panel.CustomPanelTitleBarBackground = new SolidColorBrush(Color.FromRgb(200, 220, 255));
									else if (title == "BIM - SUPPORT")
										panel.CustomPanelTitleBarBackground = new SolidColorBrush(Color.FromRgb(255, 225, 180));
									else if (title == "BIM - CHECK")
										panel.CustomPanelTitleBarBackground = new SolidColorBrush(Color.FromRgb(200, 255, 255));
									else if (title == "BIM - BOQ & EXCEL")
										panel.CustomPanelTitleBarBackground = new SolidColorBrush(Color.FromRgb(255, 255, 200));
									else if (title == "BIM - SHEET & VIEW")
										panel.CustomPanelTitleBarBackground = new SolidColorBrush(Color.FromRgb(220, 240, 255));
									else if (title == "BIM - 2D ANNOTATION")
										panel.CustomPanelTitleBarBackground = new SolidColorBrush(Color.FromRgb(240, 230, 255));
									else if (title == "BIM - TRANSFER & LINK")
										panel.CustomPanelTitleBarBackground = new SolidColorBrush(Color.FromRgb(230, 255, 230));
								}
							}
							catch { }
						}
					}
				}
			}
			catch
			{
			}
			return Result.Succeeded;
		}
		catch
		{
			return Result.Succeeded;
		}
	}

	public Result OnShutdown(UIControlledApplication application)
	{
		try { application.ControlledApplication.DocumentChanged -= DocumentTelemetryTracker.OnDocumentChanged; } catch { }
		DocumentTelemetryTracker.Stop();
		RevitMcpServer.Stop();
		return Result.Succeeded;
	}
}
