using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class ManageGridLevel : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;
		try
		{
			View activeView = doc.ActiveView;
			if (!activeView.CropBoxActive)
			{
				TaskDialog.Show("Manage Grid & Level", "The active view must have crop box enabled.");
				return Result.Cancelled;
			}
			ManageGridLevelWindow window = new ManageGridLevelWindow();
			window.OnGridVisible = delegate(bool n, bool s, bool e, bool w)
			{
				//IL_0011: Unknown result type (might be due to invalid IL or missing references)
				//IL_0017: Expected O, but got Unknown
				//IL_0019: Unknown result type (might be due to invalid IL or missing references)
				//IL_0047: Unknown result type (might be due to invalid IL or missing references)
				Transaction val = new Transaction(doc, "Grid Visible");
				try
				{
					val.Start();
					GridVisible(doc, activeView, n, s, e, w);
					val.Commit();
				}
				finally
				{
					((IDisposable)val)?.Dispose();
				}
			};
			window.OnGridFit = delegate(bool n, bool s, bool e, bool w)
			{
				//IL_0011: Unknown result type (might be due to invalid IL or missing references)
				//IL_0017: Expected O, but got Unknown
				//IL_0019: Unknown result type (might be due to invalid IL or missing references)
				//IL_0047: Unknown result type (might be due to invalid IL or missing references)
				Transaction val2 = new Transaction(doc, "Grid Fit");
				try
				{
					val2.Start();
					GridFit(doc, activeView, n, s, e, w);
					val2.Commit();
				}
				finally
				{
					((IDisposable)val2)?.Dispose();
				}
			};
			window.OnLevelFit = delegate(bool l, bool r)
			{
				//IL_0011: Unknown result type (might be due to invalid IL or missing references)
				//IL_0017: Expected O, but got Unknown
				//IL_0019: Unknown result type (might be due to invalid IL or missing references)
				//IL_0044: Unknown result type (might be due to invalid IL or missing references)
				Transaction val3 = new Transaction(doc, "Level Fit");
				try
				{
					val3.Start();
					LevelFit(doc, activeView, l, r);
					val3.Commit();
				}
				finally
				{
					((IDisposable)val3)?.Dispose();
				}
			};
			window.ShowDialog();
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

	private double GetGridHeadOffset(View view)
	{
		int scale = view.Scale;
		return 1.5 * (double)scale / 304.8;
	}

	private double GetLevelSymbolOffset(View view)
	{
		int scale = view.Scale;
		return 7.0 * (double)scale / 304.8;
	}

	private void GridVisible(Document doc, View view, bool showNorth, bool showSouth, bool showEast, bool showWest)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		List<Grid> grids = ((IEnumerable)new FilteredElementCollector(doc, ((Element)view).Id).OfCategory((BuiltInCategory)(-2000220)).WhereElementIsNotElementType()).Cast<Grid>().ToList();
		BoundingBoxXYZ cropBox = view.CropBox;
		Transform inverse = cropBox.Transform.Inverse;
		foreach (Grid grid in grids)
		{
			try
			{
				Curve curve = ((DatumPlane)grid).GetCurvesInView((DatumExtentType)1, view).FirstOrDefault();
				if ((GeometryObject)(object)curve == (GeometryObject)null)
				{
					continue;
				}
				XYZ start = curve.GetEndPoint(0);
				XYZ end = curve.GetEndPoint(1);
				XYZ startView = inverse.OfPoint(start);
				XYZ endView = inverse.OfPoint(end);
				XYZ dirView = (endView - startView).Normalize();
				if (Math.Abs(dirView.X) < Math.Abs(dirView.Y))
				{
					int southEnd;
					int northEnd;
					if (startView.Y < endView.Y)
					{
						southEnd = 0;
						northEnd = 1;
					}
					else
					{
						southEnd = 1;
						northEnd = 0;
					}
					SetBubbleVisibility(grid, view, northEnd, showNorth);
					SetBubbleVisibility(grid, view, southEnd, showSouth);
				}
				else
				{
					int westEnd;
					int eastEnd;
					if (startView.X < endView.X)
					{
						westEnd = 0;
						eastEnd = 1;
					}
					else
					{
						westEnd = 1;
						eastEnd = 0;
					}
					SetBubbleVisibility(grid, view, eastEnd, showEast);
					SetBubbleVisibility(grid, view, westEnd, showWest);
				}
			}
			catch
			{
			}
		}
	}

	private void SetBubbleVisibility(Grid grid, View view, int endIndex, bool visible)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		DatumEnds datumEnd = (DatumEnds)((endIndex != 0) ? 1 : 0);
		if (visible)
		{
			((DatumPlane)grid).ShowBubbleInView(datumEnd, view);
		}
		else
		{
			((DatumPlane)grid).HideBubbleInView(datumEnd, view);
		}
	}

	private void GridFit(Document doc, View view, bool fitNorth, bool fitSouth, bool fitEast, bool fitWest)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0300: Expected O, but got Unknown
		//IL_030b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0312: Expected O, but got Unknown
		List<Grid> grids = ((IEnumerable)new FilteredElementCollector(doc, ((Element)view).Id).OfCategory((BuiltInCategory)(-2000220)).WhereElementIsNotElementType()).Cast<Grid>().ToList();
		BoundingBoxXYZ cropBox = view.CropBox;
		Transform transform = cropBox.Transform;
		Transform inverse = transform.Inverse;
		double cropMinX = cropBox.Min.X;
		double cropMaxX = cropBox.Max.X;
		double cropMinY = cropBox.Min.Y;
		double cropMaxY = cropBox.Max.Y;
		double headOffset = GetGridHeadOffset(view);
		foreach (Grid grid in grids)
		{
			try
			{
				Curve curve = ((DatumPlane)grid).GetCurvesInView((DatumExtentType)1, view).FirstOrDefault();
				if ((GeometryObject)(object)curve == (GeometryObject)null)
				{
					continue;
				}
				XYZ start = curve.GetEndPoint(0);
				XYZ end = curve.GetEndPoint(1);
				XYZ startView = inverse.OfPoint(start);
				XYZ endView = inverse.OfPoint(end);
				XYZ dirView = (endView - startView).Normalize();
				bool isVerticalInView = Math.Abs(dirView.X) < Math.Abs(dirView.Y);
				double newStartViewX = startView.X;
				double newStartViewY = startView.Y;
				double newEndViewX = endView.X;
				double newEndViewY = endView.Y;
				bool bubble0 = ((DatumPlane)grid).IsBubbleVisibleInView((DatumEnds)0, view);
				bool bubble1 = ((DatumPlane)grid).IsBubbleVisibleInView((DatumEnds)1, view);
				if (isVerticalInView)
				{
					bool startIsSouth = startView.Y < endView.Y;
					if (fitSouth)
					{
						if ((startIsSouth || 1 == 0) ? bubble0 : bubble1)
						{
							double targetY = cropMinY - headOffset;
							if (startIsSouth)
							{
								newStartViewY = targetY;
							}
							else
							{
								newEndViewY = targetY;
							}
						}
						else
						{
							double targetY2 = cropMinY;
							if (startIsSouth)
							{
								newStartViewY = targetY2;
							}
							else
							{
								newEndViewY = targetY2;
							}
						}
					}
					if (fitNorth)
					{
						if ((!startIsSouth && 0 == 0) ? bubble0 : bubble1)
						{
							double targetY3 = cropMaxY + headOffset;
							if (startIsSouth)
							{
								newEndViewY = targetY3;
							}
							else
							{
								newStartViewY = targetY3;
							}
						}
						else
						{
							double targetY4 = cropMaxY;
							if (startIsSouth)
							{
								newEndViewY = targetY4;
							}
							else
							{
								newStartViewY = targetY4;
							}
						}
					}
				}
				else
				{
					bool startIsWest = startView.X < endView.X;
					if (fitWest)
					{
						if ((startIsWest || 1 == 0) ? bubble0 : bubble1)
						{
							double targetX = cropMinX - headOffset;
							if (startIsWest)
							{
								newStartViewX = targetX;
							}
							else
							{
								newEndViewX = targetX;
							}
						}
						else
						{
							double targetX2 = cropMinX;
							if (startIsWest)
							{
								newStartViewX = targetX2;
							}
							else
							{
								newEndViewX = targetX2;
							}
						}
					}
					if (fitEast)
					{
						if ((!startIsWest && 0 == 0) ? bubble0 : bubble1)
						{
							double targetX3 = cropMaxX + headOffset;
							if (startIsWest)
							{
								newEndViewX = targetX3;
							}
							else
							{
								newStartViewX = targetX3;
							}
						}
						else
						{
							double targetX4 = cropMaxX;
							if (startIsWest)
							{
								newEndViewX = targetX4;
							}
							else
							{
								newStartViewX = targetX4;
							}
						}
					}
				}
				XYZ newStartView = new XYZ(newStartViewX, newStartViewY, startView.Z);
				XYZ newEndView = new XYZ(newEndViewX, newEndViewY, endView.Z);
				XYZ newStartModel = transform.OfPoint(newStartView);
				XYZ newEndModel = transform.OfPoint(newEndView);
				if (newStartModel.DistanceTo(newEndModel) > 0.001)
				{
					Line newLine = Line.CreateBound(newStartModel, newEndModel);
					((DatumPlane)grid).SetCurveInView((DatumExtentType)1, view, (Curve)(object)newLine);
				}
			}
			catch
			{
			}
		}
	}

	private void LevelFit(Document doc, View view, bool fitLeft, bool fitRight)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01da: Expected O, but got Unknown
		//IL_01ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f1: Expected O, but got Unknown
		List<Level> levels = ((IEnumerable)new FilteredElementCollector(doc, ((Element)view).Id).OfCategory((BuiltInCategory)(-2000240)).WhereElementIsNotElementType()).Cast<Level>().ToList();
		BoundingBoxXYZ cropBox = view.CropBox;
		Transform transform = cropBox.Transform;
		Transform inverse = transform.Inverse;
		double cropMinX = cropBox.Min.X;
		double cropMaxX = cropBox.Max.X;
		double symbolOffset = GetLevelSymbolOffset(view);
		foreach (Level level in levels)
		{
			try
			{
				IList<Curve> curves = ((DatumPlane)level).GetCurvesInView((DatumExtentType)1, view);
				if (curves == null || curves.Count == 0)
				{
					continue;
				}
				Curve curve = curves.First();
				XYZ start = curve.GetEndPoint(0);
				XYZ end = curve.GetEndPoint(1);
				XYZ startView = inverse.OfPoint(start);
				XYZ endView = inverse.OfPoint(end);
				bool startIsLeft = startView.X < endView.X;
				bool bubble0 = ((DatumPlane)level).IsBubbleVisibleInView((DatumEnds)0, view);
				bool bubble1 = ((DatumPlane)level).IsBubbleVisibleInView((DatumEnds)1, view);
				double newStartViewX = startView.X;
				double newEndViewX = endView.X;
				if (fitLeft)
				{
					if ((startIsLeft || 1 == 0) ? bubble0 : bubble1)
					{
						double targetX = cropMinX - symbolOffset;
						if (startIsLeft)
						{
							newStartViewX = targetX;
						}
						else
						{
							newEndViewX = targetX;
						}
					}
					else
					{
						double targetX2 = cropMinX;
						if (startIsLeft)
						{
							newStartViewX = targetX2;
						}
						else
						{
							newEndViewX = targetX2;
						}
					}
				}
				if (fitRight)
				{
					if ((!startIsLeft && 0 == 0) ? bubble0 : bubble1)
					{
						double targetX3 = cropMaxX + symbolOffset;
						if (startIsLeft)
						{
							newEndViewX = targetX3;
						}
						else
						{
							newStartViewX = targetX3;
						}
					}
					else
					{
						double targetX4 = cropMaxX;
						if (startIsLeft)
						{
							newEndViewX = targetX4;
						}
						else
						{
							newStartViewX = targetX4;
						}
					}
				}
				XYZ newStartView = new XYZ(newStartViewX, startView.Y, startView.Z);
				XYZ newEndView = new XYZ(newEndViewX, endView.Y, endView.Z);
				XYZ newStartModel = transform.OfPoint(newStartView);
				XYZ newEndModel = transform.OfPoint(newEndView);
				if (newStartModel.DistanceTo(newEndModel) > 0.001)
				{
					Line newLine = Line.CreateBound(newStartModel, newEndModel);
					((DatumPlane)level).SetCurveInView((DatumExtentType)1, view, (Curve)(object)newLine);
				}
			}
			catch
			{
			}
		}
	}
}
