using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;

namespace BIN;

public static class NaviateHelper
{
	public static Connector[] ConnectorArray(Element element)
	{
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Expected O, but got Unknown
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Invalid comparison between Unknown and I4
		if (element == null)
		{
			return null;
		}
		ConnectorSet connSet = GetConnectorSet(element);
		if (connSet == null)
		{
			return null;
		}
		List<Connector> list = new List<Connector>();
		foreach (Connector item in connSet)
		{
			Connector c = item;
			if ((int)c.Domain != 5)
			{
				list.Add(c);
			}
		}
		return list.ToArray();
	}

	public static Connector[] ConnectorArrayUnused(Element element)
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Expected O, but got Unknown
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Invalid comparison between Unknown and I4
		if (element == null)
		{
			return null;
		}
		ConnectorSet connSet = GetConnectorSet(element);
		if (connSet == null)
		{
			return null;
		}
		List<Connector> list = new List<Connector>();
		foreach (Connector item in connSet)
		{
			Connector c = item;
			if ((int)c.Domain != 5 && !c.IsConnected)
			{
				list.Add(c);
			}
		}
		return (list.Count == 0) ? null : list.ToArray();
	}

	public static Connector[] ClosestConnectors(Element element1, Element element2, bool align)
	{
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		Connector[] result = (Connector[])(object)new Connector[2];
		Connector[] arr1 = ConnectorArrayUnused(element1);
		if (arr1 == null)
		{
			return null;
		}
		Connector[] arr2 = ConnectorArrayUnused(element2);
		if (arr2 == null)
		{
			return null;
		}
		Connector c1 = (OnlyOneDomain(arr2) ? ClosestConnectorOfDomain(arr1, arr2, arr2[0].Domain) : ClosestAvailableConnector(arr1, arr2));
		if (c1 == null)
		{
			return null;
		}
		Connector c2 = (align ? ClosestConnectorOfDomain(arr2, arr1, c1.Domain) : ClosestConnectorOfDomainAndAngle(arr2, c1));
		if (c2 == null)
		{
			return null;
		}
		result[0] = c1;
		result[1] = c2;
		return result;
	}

	public static Connector NearestConnector(Connector[] cA, XYZ startPoint)
	{
		if (cA == null)
		{
			return null;
		}
		if (cA.Length == 1)
		{
			return cA[0];
		}
		Connector result = null;
		double minDist = double.MaxValue;
		for (int i = 0; i < cA.Length; i++)
		{
			double dist = cA[i].Origin.DistanceTo(startPoint);
			if (dist < minDist)
			{
				minDist = dist;
				result = cA[i];
			}
		}
		return result;
	}

	public static Connector FarthestConnector(Connector[] cA, XYZ startPoint)
	{
		if (cA == null)
		{
			return null;
		}
		Connector result = null;
		double maxDist = double.MinValue;
		for (int i = 0; i < cA.Length; i++)
		{
			double dist = cA[i].Origin.DistanceTo(startPoint);
			if (dist > maxDist)
			{
				maxDist = dist;
				result = cA[i];
			}
		}
		return result;
	}

	public static Connector ClosestAvailableConnector(Connector[] cA1, Connector[] cA2)
	{
		Connector result = null;
		double minDist = double.MaxValue;
		for (int i = 0; i < cA1.Length; i++)
		{
			if (!cA1[i].IsConnected)
			{
				if (cA1[i].Origin.IsAlmostEqualTo(cA2[0].Origin))
				{
					return cA1[i];
				}
				double dist = cA1[i].Origin.DistanceTo(cA2[0].Origin);
				if (dist < minDist)
				{
					minDist = dist;
					result = cA1[i];
				}
			}
		}
		return result;
	}

	public static Connector ClosestConnectorOfDomain(Connector[] cA1, Connector[] cA2, Domain domain)
	{
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		Connector result = null;
		double minDist = double.MaxValue;
		for (int i = 0; i < cA1.Length; i++)
		{
			if (!cA1[i].IsConnected)
			{
				double dist = cA1[i].Origin.DistanceTo(cA2[0].Origin);
				if (dist < minDist && cA1[i].Domain == domain)
				{
					minDist = dist;
					result = cA1[i];
				}
			}
		}
		return result;
	}

	public static Connector ClosestConnectorOfDomainAndAngle(Connector[] cA1, Connector c)
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		Connector result = null;
		double minDist = double.MaxValue;
		for (int i = 0; i < cA1.Length; i++)
		{
			if (!cA1[i].IsConnected)
			{
				double dist = cA1[i].Origin.DistanceTo(c.Origin);
				bool sameDomain = cA1[i].Domain == c.Domain;
				bool oppositeDir = c.CoordinateSystem.BasisZ.DotProduct(cA1[i].CoordinateSystem.BasisZ) < -0.9;
				if (dist < minDist && sameDomain && oppositeDir)
				{
					minDist = dist;
					result = cA1[i];
				}
			}
		}
		return result;
	}

	public static Connector ConnectedConnector(Connector cInput, XYZ origin)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Expected O, but got Unknown
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Invalid comparison between Unknown and I4
		foreach (Connector allRef in cInput.AllRefs)
		{
			Connector c = allRef;
			if ((int)c.ConnectorType == 1 && c.IsConnected && Math.Round(c.Origin.X, 2) == Math.Round(origin.X, 2) && Math.Round(c.Origin.Y, 2) == Math.Round(origin.Y, 2))
			{
				return c;
			}
		}
		return null;
	}

	public static bool OnlyOneDomain(Connector[] cA)
	{
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		if (cA.Length <= 1)
		{
			return true;
		}
		List<string> domains = new List<string>();
		foreach (Connector c in cA)
		{
			Domain domain = c.Domain;
			domains.Add(domain.ToString());
		}
		return domains.Distinct().Count() == 1;
	}

	public static ElementId GetLevel(Document doc, double z)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Expected O, but got Unknown
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Expected O, but got Unknown
		try
		{
			return ((Element)doc.ActiveView.GenLevel).Id;
		}
		catch
		{
			FilteredElementCollector collector = new FilteredElementCollector(doc).OfClass(typeof(Level));
			Level bestLevel = null;
			double minDiff = double.MaxValue;
			foreach (Level item in collector)
			{
				Level lvl = item;
				if (lvl.Elevation <= z && z - lvl.Elevation < minDiff)
				{
					minDiff = z - lvl.Elevation;
					bestLevel = lvl;
				}
			}
			if (bestLevel == null)
			{
				bestLevel = (Level)collector.FirstElement();
			}
			return ((Element)bestLevel).Id;
		}
	}

	public static MechanicalSystemType GetMechanicalSystem(Document doc, Connector connector)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Invalid comparison between Unknown and I4
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Expected I4, but got Unknown
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Expected O, but got Unknown
		//IL_01bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c6: Expected O, but got Unknown
		//IL_02e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ea: Expected O, but got Unknown
		if ((int)connector.Domain != 1)
		{
			return null;
		}
		try
		{
			MEPSystem mEPSystem = connector.MEPSystem;
			MechanicalSystem mechanicalSystem = (MechanicalSystem)(object)((mEPSystem is MechanicalSystem) ? mEPSystem : null);
			if (mechanicalSystem != null)
			{
				ElementId typeId = ((Element)mechanicalSystem).GetTypeId();
				Element element = doc.GetElement(typeId);
				return (MechanicalSystemType)(object)((element is MechanicalSystemType) ? element : null);
			}
		}
		catch
		{
		}
		FilteredElementCollector collector = new FilteredElementCollector(doc).OfClass(typeof(MechanicalSystemType));
		switch ((int)connector.DuctSystemType)
		{
		case 1:
			foreach (MechanicalSystemType item in collector)
			{
				MechanicalSystemType sysType3 = item;
				string name3 = ((Element)sysType3).Name;
				if (name3.Contains("Supply Air") || name3.Contains("Zuluft") || name3.Contains("Soufflage") || name3.Contains("Aria di mandata") || name3.Contains("給気") || name3.Contains("Powietrze nawiewane") || name3.Contains("Приточный воздух") || name3.Contains("공급 공기") || name3.Contains("Suministro de aire") || name3.Contains("Suprimento de ar") || name3.Contains("送风") || name3.Contains("進氣") || name3.Contains("Přívod vzduchu"))
				{
					return sysType3;
				}
			}
			break;
		case 2:
			foreach (MechanicalSystemType item2 in collector)
			{
				MechanicalSystemType sysType2 = item2;
				string name2 = ((Element)sysType2).Name;
				if (name2.Contains("Return Air") || name2.Contains("Umluft") || name2.Contains("Reprise") || name2.Contains("Aria di ritorno") || name2.Contains("還気") || name2.Contains("Powietrze recyrkulac.") || name2.Contains("Рециркуliрующий воздух") || name2.Contains("순환 공기") || name2.Contains("Aire de retorno") || name2.Contains("Ar de retorno") || name2.Contains("回风") || name2.Contains("回氣") || name2.Contains("Zpětný vzduch"))
				{
					return sysType2;
				}
			}
			break;
		case 3:
			foreach (MechanicalSystemType item3 in collector)
			{
				MechanicalSystemType sysType = item3;
				string name = ((Element)sysType).Name;
				if (name.Contains("Exhaust Air") || name.Contains("Abluft") || name.Contains("Extraction d'air") || name.Contains("Aria di scarico") || name.Contains("排気") || name.Contains("Powietrze zwracane") || name.Contains("Отработанный воздух") || name.Contains("배기") || name.Contains("Aire viciado") || name.Contains("Ar de exaustão") || name.Contains("排风") || name.Contains("排出氣") || name.Contains("Odváděný vzduch"))
				{
					return sysType;
				}
			}
			break;
		}
		return ((IEnumerable)collector).Cast<MechanicalSystemType>().FirstOrDefault();
	}

	public static PipingSystemType GetPipeSystem(Document doc, Connector connector)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Invalid comparison between Unknown and I4
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Expected I4, but got Unknown
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Expected O, but got Unknown
		if ((int)connector.Domain != 3)
		{
			return null;
		}
		try
		{
			MEPSystem mEPSystem = connector.MEPSystem;
			PipingSystem pipingSystem = (PipingSystem)(object)((mEPSystem is PipingSystem) ? mEPSystem : null);
			if (pipingSystem != null)
			{
				ElementId typeId = ((Element)pipingSystem).GetTypeId();
				Element element = doc.GetElement(typeId);
				return (PipingSystemType)(object)((element is PipingSystemType) ? element : null);
			}
		}
		catch
		{
		}
		FilteredElementCollector collector = new FilteredElementCollector(doc).OfClass(typeof(PipingSystemType));
		int sysInt = (int)connector.PipeSystemType;
		foreach (PipingSystemType item in collector)
		{
			PipingSystemType sysType = item;
			string name = ((Element)sysType).Name;
			if (sysInt == 7 && (name.Contains("Hydronic Supply") || name.Contains("Vorlauf") || name.Contains("Alimentation hydraulique") || name.Contains("Mandata di sistema idronico") || name.Contains("温水循環(往)") || name.Contains("Zasilanie wody") || name.Contains("Приточная жидкость") || name.Contains("순환수 공급") || name.Contains("Suministro hidrónico") || name.Contains("Suprimento hidrônico") || name.Contains("循环供水") || name.Contains("Přívod teplé vody")))
			{
				return sysType;
			}
			if (sysInt == 8 && (name.Contains("Hydronic Return") || name.Contains("Rücklauf") || name.Contains("Retour hydraulique") || name.Contains("Ritorno di sistema idronico") || name.Contains("温水循環(還)") || name.Contains("Zwrot wody") || name.Contains("Обратная жидкость") || name.Contains("순환수 순환") || name.Contains("Retorno hidrónico") || name.Contains("Retorno hidrônico") || name.Contains("循环回水") || name.Contains("Zpětné vedení teplé vody")))
			{
				return sysType;
			}
			if (sysInt == 16 && (name.Contains("Sanitary") || name.Contains("Abwasser") || name.Contains("Sanitaire") || name.Contains("Acque reflue") || name.Contains("排水") || name.Contains("Sanitarny") || name.Contains("Канализация") || name.Contains("위생") || name.Contains("Sanitario") || name.Contains("Sanitário") || name.Contains("卫生设备") || name.Contains("Sanitární")))
			{
				return sysType;
			}
			if (sysInt == 19 && (name.Contains("Domestic Hot Water") || name.Contains("Warmwasser") || name.Contains("Eau chaude sanitaire") || name.Contains("Acqua calda sanitaria") || name.Contains("屋内給湯") || name.Contains("Domowa woda gorąca") || name.Contains("주택용 온수") || name.Contains("Agua caliente doméstica") || name.Contains("Água quente residencial") || name.Contains("家用热水") || name.Contains("Teplá voda v domácnosti")))
			{
				return sysType;
			}
			if (sysInt == 20 && (name.Contains("Domestic Cold Water") || name.Contains("Kaltwasser") || name.Contains("Eau froide sanitaire") || name.Contains("Acqua fredda sanitaria") || name.Contains("屋内給水") || name.Contains("Domowa woda zimna") || name.Contains("주택용 냉수") || name.Contains("Agua fría doméstica") || name.Contains("Água fría residencial") || name.Contains("家用 cold water") || name.Contains("Studená voda v domácnosti")))
			{
				return sysType;
			}
			if (sysInt == 23 && (name.Contains("Fire Protection Wet") || name.Contains("Brandschutz - Nass") || name.Contains("Système sous eau de protection contre les incendies") || name.Contains("Protezione antincendio a umido") || name.Contains("湿式防火") || name.Contains("Mokra ochrona ppoż.") || name.Contains("Водяная sistema пожароtušenja") || name.Contains("습식 방화") || name.Contains("Protección contra incendios húmeda") || name.Contains("Proteção contra incêndio a água") || name.Contains("湿式消防") || name.Contains("Požární ochrana – vodní средствa")))
			{
				return sysType;
			}
			if (sysInt == 17 && (name.Contains("Vent") || name.Contains("Belüftung") || name.Contains("Aération") || name.Contains("Ventilazione") || name.Contains("排気") || name.Contains("Wentylacyjny") || name.Contains("Вентиляционное отверстие") || name.Contains("통기") || name.Contains("Ventilación") || name.Contains("Ventilação") || name.Contains("通气管") || name.Contains("Průduch")))
			{
				return sysType;
			}
			if (sysInt == 22 && (name.Contains("Other") || name.Contains("Sonstige") || name.Contains("Autre") || name.Contains("Altro") || name.Contains("その他") || name.Contains("Inne") || name.Contains("Прочее") || name.Contains("기타") || name.Contains("Otro") || name.Contains("Outro") || name.Contains("其他") || name.Contains("Ostatní")))
			{
				return sysType;
			}
		}
		return ((IEnumerable)collector).Cast<PipingSystemType>().FirstOrDefault();
	}

	public static double GetExtensionLength(Connector connector)
	{
		if (connector.Owner is Duct duct)
			return DuctProfileData.Read(duct, connector).ExtensionLength;
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Expected I4, but got Unknown
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Expected I4, but got Unknown
		double result = 1.0;
		int domainInt = (int)connector.Domain;
		int shapeInt = (int)connector.Shape;
		if (domainInt == 1 && shapeInt == 1)
		{
			result = 4.0 * connector.Width;
		}
		else if (domainInt == 1 && shapeInt == 2)
		{
			result = 4.0 * connector.Width;
		}
		else if (domainInt == 1 && shapeInt == 0)
		{
			result = 4.0 * connector.Radius;
		}
		else
		{
			switch (domainInt)
			{
			case 4:
				result = 3.0;
				break;
			case 3:
				result = 7.0 * connector.Radius;
				break;
			}
		}
		return result;
	}

	public static void DeleteSystems(Document doc, List<ElementId> selectedElements)
	{
		List<ElementId> systemIds = new List<ElementId>();
		foreach (ElementId eid in selectedElements)
		{
			Element elem = doc.GetElement(eid);
			if (elem == null)
			{
				continue;
			}
			Connector[] connectors = ConnectorArray(elem);
			if (connectors == null)
			{
				continue;
			}
			Connector[] array = connectors;
			foreach (Connector c in array)
			{
				if (c.MEPSystem != null)
				{
					ElementId sysId = ((Element)c.MEPSystem).Id;
					if (!systemIds.Contains(sysId))
					{
						systemIds.Add(sysId);
					}
				}
			}
		}
		foreach (ElementId sysId2 in systemIds)
		{
			try
			{
				doc.Delete(sysId2);
				SimpleLogger.Log($"Deleted system: {ElementIdHelper.GetIdValue(sysId2)}");
			}
			catch (Exception ex)
			{
				SimpleLogger.Log($"Could not delete system {ElementIdHelper.GetIdValue(sysId2)}: {ex.Message}");
			}
		}
	}

	public static void AlignColinearMEPElements(Element movingElement, Connector stationaryConn, Connector movingConn, Document doc)
	{
		XYZ moveVec = stationaryConn.Origin - movingConn.Origin;
		ElementTransformUtils.MoveElement(doc, movingElement.Id, moveVec);
	}

	public static void AlignIntersectingMEPElements(Element movingElement, Connector stationaryClosest, Connector movingClosest, Connector stationaryFarthest, Connector movingFarthest, Document doc)
	{
		XYZ moveVec = stationaryClosest.Origin - movingClosest.Origin;
		ElementTransformUtils.MoveElement(doc, movingElement.Id, moveVec);
		XYZ stDir = stationaryClosest.CoordinateSystem.BasisZ;
		XYZ mvDir = movingClosest.CoordinateSystem.BasisZ;
		double dot = stDir.DotProduct(mvDir);
		if (Math.Abs(dot + 1.0) > 0.001)
		{
			XYZ cross = stDir.CrossProduct(mvDir);
			if (cross.GetLength() > 0.001)
			{
				Line rotAxis = Line.CreateBound(stationaryClosest.Origin, stationaryClosest.Origin + cross.Normalize());
				double angle = Math.PI - stDir.AngleTo(mvDir);
				ElementTransformUtils.RotateElement(doc, movingElement.Id, rotAxis, angle);
			}
		}
	}

	public static XYZ PerpIntersection(XYZ p0, XYZ p1, XYZ pX)
	{
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Expected O, but got Unknown
		double num = (pX.X - p0.X) * (p1.X - p0.X) + (pX.Y - p0.Y) * (p1.Y - p0.Y) + (pX.Z - p0.Z) * (p1.Z - p0.Z);
		double denom = p0.DistanceTo(p1);
		num /= denom * denom;
		return new XYZ(p0.X + num * (p1.X - p0.X), p0.Y + num * (p1.Y - p0.Y), p0.Z + num * (p1.Z - p0.Z));
	}

	public static XYZ Midpoint(XYZ p0, XYZ p1)
	{
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Expected O, but got Unknown
		return new XYZ((p0.X + p1.X) / 2.0, (p0.Y + p1.Y) / 2.0, (p0.Z + p1.Z) / 2.0);
	}

	public static XYZ ProjectPointOnPlane(XYZ planeNormal, XYZ anyPointOnPlane, XYZ pointToProject)
	{
		double dist = planeNormal.X * (pointToProject.X - anyPointOnPlane.X) + planeNormal.Y * (pointToProject.Y - anyPointOnPlane.Y) + planeNormal.Z * (pointToProject.Z - anyPointOnPlane.Z);
		XYZ offset = planeNormal.Normalize() * (0.0 - dist);
		return pointToProject + offset;
	}

	public static XYZ GetIntersection(Line line1, Line line2)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Invalid comparison between Unknown and I4
		IntersectionResultArray results = null;
		SetComparisonResult scr = ((Curve)line1).Intersect((Curve)(object)line2, out results);
		if ((int)scr != 8 || results == null || results.Size != 1)
		{
			return null;
		}
		return results.get_Item(0).XYZPoint;
	}

	public static XYZ IntersectionTwoVectors(XYZ upBranch, XYZ downBranch, XYZ downMain, XYZ upMain)
	{
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Expected O, but got Unknown
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Expected O, but got Unknown
		//IL_0179: Unknown result type (might be due to invalid IL or missing references)
		//IL_0180: Expected O, but got Unknown
		XYZ d1 = upMain - downMain;
		XYZ d2 = upBranch - downBranch;
		XYZ d3 = downMain - downBranch;
		double a = d1.DotProduct(d1);
		double b = d1.DotProduct(d2);
		double c = d2.DotProduct(d2);
		double d4 = d1.DotProduct(d3);
		double e = d2.DotProduct(d3);
		double denom = a * c - b * b;
		double sc;
		double tc;
		if (denom < 1E-08)
		{
			sc = 0.0;
			tc = ((b > c) ? (d4 / b) : (e / c));
		}
		else
		{
			sc = (b * e - c * d4) / denom;
			tc = (a * e - b * d4) / denom;
		}
		XYZ p1 = new XYZ(downMain.X + sc * d1.X, downMain.Y + sc * d1.Y, downMain.Z + sc * d1.Z);
		XYZ p2 = new XYZ(downBranch.X + tc * d2.X, downBranch.Y + tc * d2.Y, downBranch.Z + tc * d2.Z);
		return new XYZ((p2.X - p1.X) / 2.0 + p1.X, (p2.Y - p1.Y) / 2.0 + p1.Y, (p2.Z - p1.Z) / 2.0 + p1.Z);
	}

	public static void DrawCableTray(Document doc, Reference selectedReference, XYZ selectedPoint, Connector closestConnectorSource, XYZ start, XYZ end, ElementId levelId)
	{
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Expected O, but got Unknown
		//IL_015f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Expected O, but got Unknown
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			Element sourceElement = doc.GetElement(selectedReference);
			CableTray oldTray = (CableTray)(object)((sourceElement is CableTray) ? sourceElement : null);
			if (oldTray == null)
			{
				return;
			}
			ElementId typeId = ((Element)oldTray).GetTypeId();
			double width = ((Element)oldTray).get_Parameter((BuiltInParameter)(-1140122)).AsDouble();
			double height = ((Element)oldTray).get_Parameter((BuiltInParameter)(-1140121)).AsDouble();
			SubTransaction sT = new SubTransaction(doc);
			CableTray newTray;
			try
			{
				sT.Start();
				newTray = CableTray.Create(doc, typeId, start, end, levelId);
				((Element)newTray).get_Parameter((BuiltInParameter)(-1140122)).Set(width);
				((Element)newTray).get_Parameter((BuiltInParameter)(-1140121)).Set(height);
				sT.Commit();
			}
			finally
			{
				((IDisposable)sT)?.Dispose();
			}
			doc.Regenerate();
			Connector connSource = ((IEnumerable)GetConnectorSet(sourceElement)).Cast<Connector>().FirstOrDefault((Connector c) => c.Origin.DistanceTo(start) < 0.001);
			Connector connNew = ((IEnumerable)GetConnectorSet((Element)(object)newTray)).Cast<Connector>().FirstOrDefault((Connector c) => c.Origin.DistanceTo(start) < 0.001);
			if (connSource == null || connNew == null)
			{
				return;
			}
			SubTransaction sT2 = new SubTransaction(doc);
			try
			{
				sT2.Start();
				try
				{
					FamilyInstance newElbow = doc.Create.NewElbowFitting(connSource, connNew);
					CopyComments((Element)(object)oldTray, (Element)(object)newTray, newElbow);
				}
				catch
				{
					try
					{
						connSource.ConnectTo(connNew);
					}
					catch
					{
					}
				}
				sT2.Commit();
			}
			finally
			{
				((IDisposable)sT2)?.Dispose();
			}
		}
		catch (Exception ex)
		{
			SimpleLogger.Log("DrawCableTray Error: " + ex.Message);
		}
	}

	public static void DrawConduit(Document doc, Reference selectedReference, XYZ selectedPoint, Connector closestConnectorSource, XYZ start, XYZ end, ElementId levelId)
	{
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Expected O, but got Unknown
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Expected O, but got Unknown
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			Element sourceElement = doc.GetElement(selectedReference);
			Conduit oldConduit = (Conduit)(object)((sourceElement is Conduit) ? sourceElement : null);
			if (oldConduit == null)
			{
				return;
			}
			ElementId typeId = ((Element)oldConduit).GetTypeId();
			double diameter = ((MEPCurve)oldConduit).Diameter;
			SubTransaction sT = new SubTransaction(doc);
			Conduit newConduit;
			try
			{
				sT.Start();
				newConduit = Conduit.Create(doc, typeId, start, end, levelId);
				((Element)newConduit).get_Parameter((BuiltInParameter)(-1140123)).Set(diameter);
				sT.Commit();
			}
			finally
			{
				((IDisposable)sT)?.Dispose();
			}
			doc.Regenerate();
			Connector connSource = ((IEnumerable)GetConnectorSet(sourceElement)).Cast<Connector>().FirstOrDefault((Connector c) => c.Origin.DistanceTo(start) < 0.001);
			Connector connNew = ((IEnumerable)GetConnectorSet((Element)(object)newConduit)).Cast<Connector>().FirstOrDefault((Connector c) => c.Origin.DistanceTo(start) < 0.001);
			if (connSource == null || connNew == null)
			{
				return;
			}
			SubTransaction sT2 = new SubTransaction(doc);
			try
			{
				sT2.Start();
				try
				{
					FamilyInstance newElbow = doc.Create.NewElbowFitting(connSource, connNew);
					CopyComments((Element)(object)oldConduit, (Element)(object)newConduit, newElbow);
				}
				catch
				{
					try
					{
						connSource.ConnectTo(connNew);
					}
					catch
					{
					}
				}
				sT2.Commit();
			}
			finally
			{
				((IDisposable)sT2)?.Dispose();
			}
		}
		catch (Exception ex)
		{
			SimpleLogger.Log("DrawConduit Error: " + ex.Message);
		}
	}

	public static void DrawDuct(Document doc, Reference selectedReference, XYZ selectedPoint, Connector closestConnectorSource, XYZ start, XYZ end)
	{
		Duct source = (selectedReference != null ? doc.GetElement(selectedReference) : closestConnectorSource?.Owner) as Duct;
		if (source == null)
			throw new System.InvalidOperationException("The selected element is not a duct.");
		DuctElbowBuilder.Build(doc, source, closestConnectorSource, start, end);
	}

	public static Pipe DrawPipeWithElbow(Document doc, Reference selectedReference, XYZ selectedPoint, Connector closestConnectorSource, XYZ start, XYZ end)
	{
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Expected O, but got Unknown
		//IL_0155: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Expected O, but got Unknown
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			Element sourceElement = (selectedReference != null) ? doc.GetElement(selectedReference) : closestConnectorSource?.Owner;
			Pipe oldPipe = sourceElement as Pipe;
			if (oldPipe == null)
			{
				return null;
			}
			PipeType pipeType = oldPipe.PipeType;
			double diameter = ((MEPCurve)oldPipe).Diameter;
			PipingSystemType pipeSystem = GetPipeSystem(doc, closestConnectorSource);
			ElementId level = GetLevel(doc, selectedPoint != null ? selectedPoint.Z : start.Z);
			SubTransaction sT = new SubTransaction(doc);
			Pipe newPipe;
			try
			{
				sT.Start();
				newPipe = Pipe.Create(doc, ((Element)pipeSystem).Id, ((Element)pipeType).Id, level, start, end);
				((Element)newPipe).get_Parameter((BuiltInParameter)(-1140225)).Set(diameter);
				sT.Commit();
			}
			finally
			{
				((IDisposable)sT)?.Dispose();
			}
			doc.Regenerate();
			Connector connSource = ((IEnumerable)GetConnectorSet(sourceElement)).Cast<Connector>().FirstOrDefault((Connector c) => c.Origin.DistanceTo(start) < 0.001);
			Connector connNew = ((IEnumerable)GetConnectorSet((Element)(object)newPipe)).Cast<Connector>().FirstOrDefault((Connector c) => c.Origin.DistanceTo(start) < 0.001);
			if (connSource != null && connNew != null)
			{
				SubTransaction sT2 = new SubTransaction(doc);
				try
				{
					sT2.Start();
					try
					{
						FamilyInstance newElbow = doc.Create.NewElbowFitting(connSource, connNew);
						CopyComments((Element)(object)oldPipe, (Element)(object)newPipe, newElbow);
					}
					catch
					{
						try
						{
							connSource.ConnectTo(connNew);
						}
						catch
						{
						}
					}
					sT2.Commit();
				}
				finally
				{
					((IDisposable)sT2)?.Dispose();
				}
			}
			return newPipe;
		}
		catch (Exception ex)
		{
			SimpleLogger.Log("DrawPipeWithElbow Error: " + ex.Message);
			return null;
		}
	}

	public static void CopyComments(Element source, Element targetElement, FamilyInstance targetFitting)
	{
		try
		{
			Parameter p = source.LookupParameter("Comments");
			string comments = ((p != null) ? (p.AsString() ?? "") : "");
			if (targetElement != null)
			{
				Parameter obj = targetElement.LookupParameter("Comments");
				if (obj != null)
				{
					obj.Set(comments);
				}
			}
			if (targetFitting != null)
			{
				Parameter obj2 = ((Element)targetFitting).LookupParameter("Comments");
				if (obj2 != null)
				{
					obj2.Set(comments);
				}
			}
		}
		catch
		{
		}
	}

	public static void CreateElbowPipeFitting(Document doc, Pipe pipe1, Pipe pipe2)
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Expected O, but got Unknown
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Expected O, but got Unknown
		ConnectorSet cS1 = GetConnectorSet((Element)(object)pipe1);
		ConnectorSet cS2 = GetConnectorSet((Element)(object)pipe2);
		if (cS1 == null || cS2 == null)
		{
			return;
		}
		List<Connector> list = new List<Connector>();
		foreach (Connector item in cS1)
		{
			Connector c1 = item;
			foreach (Connector item2 in cS2)
			{
				Connector c2 = item2;
				if (Math.Round(c1.Origin.DistanceTo(c2.Origin), 3) == 0.0)
				{
					list.Add(c1);
					list.Add(c2);
					break;
				}
			}
		}
		if (list.Count != 2)
		{
			return;
		}
		try
		{
			doc.Create.NewElbowFitting(list[0], list[1]);
		}
		catch
		{
		}
	}

	public static ConnectorSet GetConnectorSet(Element element)
	{
		FamilyInstance fi = (FamilyInstance)(object)((element is FamilyInstance) ? element : null);
		if (fi != null && fi.MEPModel != null)
		{
			return fi.MEPModel.ConnectorManager.Connectors;
		}
		MEPCurve curve = (MEPCurve)(object)((element is MEPCurve) ? element : null);
		if (curve != null)
		{
			return curve.ConnectorManager.Connectors;
		}
		FabricationPart fab = (FabricationPart)(object)((element is FabricationPart) ? element : null);
		if (fab != null)
		{
			return fab.ConnectorManager.Connectors;
		}
		return null;
	}

	public static ConnectorSet GetUnusedConnectorSet(Element element)
	{
		FamilyInstance fi = (FamilyInstance)(object)((element is FamilyInstance) ? element : null);
		if (fi != null && fi.MEPModel != null)
		{
			return fi.MEPModel.ConnectorManager.UnusedConnectors;
		}
		MEPCurve curve = (MEPCurve)(object)((element is MEPCurve) ? element : null);
		if (curve != null)
		{
			return curve.ConnectorManager.UnusedConnectors;
		}
		FabricationPart fab = (FabricationPart)(object)((element is FabricationPart) ? element : null);
		if (fab != null)
		{
			return fab.ConnectorManager.UnusedConnectors;
		}
		return null;
	}
}
