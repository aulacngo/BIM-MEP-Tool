using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace BIN;

public class BOQWindow : Window, IComponentConnector, IStyleConnector
{
	private Document _doc;

	public static ObservableCollection<string> StaticHVACGasConduitTypes;

	private static readonly string SettingsFilePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Autodesk", "Revit", "Addins", "BIN_BOQSettings.json");

	private bool _isUpdatingCheckboxes = false;

	private bool _isSyncingRadioButtons = false;

	internal Button BtnMaximize;

	internal System.Windows.Shapes.Path PathMaximize;

	internal System.Windows.Shapes.Path PathRestore;

	internal RadioButton RbAll;

	internal RadioButton RbInView;

	internal ComboBox CmbCongTac;

	internal StackPanel PanelDien;

	internal RadioButton RbServiceType;

	internal RadioButton RbCableTrayType;

	internal RadioButton RbBuswayOption1;

	internal RadioButton RbBuswayOption2;

	internal RadioButton RbConduitServiceType;

	internal RadioButton RbConduitType;

	internal StackPanel PanelCapThoatNuoc;

	internal StackPanel PanelHVAC;

	internal RadioButton RbHvacDuctAggregate;

	internal RadioButton RbHvacDuctNoAggregate;

	internal RadioButton RbHvacDuctOpt3;

	internal StackPanel PanelChuaChay;

	internal CheckBox ChkSysAll;

	internal CheckBox ChkSysDien;

	internal CheckBox ChkDienCableTray;

	internal CheckBox ChkDienBusway;

	internal CheckBox ChkDienConduit;

	internal CheckBox ChkSysNuoc;

	internal CheckBox ChkNuocCap;

	internal CheckBox ChkNuocThoat;

	internal CheckBox ChkSysHvac;

	internal CheckBox ChkHvacDuct;

	internal CheckBox ChkHvacPipe;

	internal CheckBox ChkHvacGas;

	internal CheckBox ChkSysPccc;

	internal CheckBox ChkPcccPipe;

	internal TextBlock TxtStatus;

	internal ProgressBar MainProgressBar;

	internal TextBox TxtSavePath;

	internal CheckBox CheckAutoOpen;

	internal CheckBox CheckIncludeFileLink;

	private bool _contentLoaded;

	public ObservableCollection<BOQCategoryItem> Categories { get; set; }

	public ObservableCollection<string> CableTrayTypes { get; set; } = new ObservableCollection<string>();

	public ObservableCollection<string> CableTrayServiceTypes { get; set; } = new ObservableCollection<string>();

	public ObservableCollection<string> CableTrayFilterSource { get; set; } = new ObservableCollection<string>();

	public ObservableCollection<string> ConduitTypes { get; set; } = new ObservableCollection<string>();

	public ObservableCollection<string> ConduitServiceTypes { get; set; } = new ObservableCollection<string>();

	public ObservableCollection<string> ConduitFilterSource { get; set; } = new ObservableCollection<string>();

	public ObservableCollection<string> HVACGasConduitTypes { get; set; } = new ObservableCollection<string>();

	public ObservableCollection<string> PipeTypes { get; set; } = new ObservableCollection<string>();

	public ObservableCollection<string> DuctTypes { get; set; } = new ObservableCollection<string>();

	public ObservableCollection<string> PipeSystemTypes { get; set; } = new ObservableCollection<string>();

	public ObservableCollection<string> DuctSystemTypes { get; set; } = new ObservableCollection<string>();

	public List<string> FilterOptions { get; set; } = new List<string> { "contain", "doesn't contain", "equal", "not equal" };

	public ObservableCollection<BOQFilterItem> DienCableTraySystems { get; set; } = new ObservableCollection<BOQFilterItem>();

	public ObservableCollection<BOQGroupItem> DienCableTrayGroups { get; set; } = new ObservableCollection<BOQGroupItem>();

	public ObservableCollection<BOQFilterItem> DienBuswaySystems { get; set; } = new ObservableCollection<BOQFilterItem>();

	public ObservableCollection<BOQGroupItem> DienBuswayGroups { get; set; } = new ObservableCollection<BOQGroupItem>();

	public ObservableCollection<BOQFilterItem> DienConduitSystems { get; set; } = new ObservableCollection<BOQFilterItem>();

	public ObservableCollection<BOQGroupItem> DienConduitGroups { get; set; } = new ObservableCollection<BOQGroupItem>();

	public ObservableCollection<BOQFilterItem> CapThoatNuocSystems { get; set; } = new ObservableCollection<BOQFilterItem>();

	public ObservableCollection<BOQGroupItem> CapThoatNuocGroups { get; set; } = new ObservableCollection<BOQGroupItem>();

	public ObservableCollection<BOQGroupItem> CapNuocGroups { get; set; } = new ObservableCollection<BOQGroupItem>();

	public ObservableCollection<BOQGroupItem> ThoatNuocGroups { get; set; } = new ObservableCollection<BOQGroupItem>();

	public ObservableCollection<BOQFilterItem> HVACPipeSystems { get; set; } = new ObservableCollection<BOQFilterItem>();

	public ObservableCollection<BOQGroupItem> HVACPipeGroups { get; set; } = new ObservableCollection<BOQGroupItem>();

	public ObservableCollection<BOQFilterItem> HVACDuctSystems { get; set; } = new ObservableCollection<BOQFilterItem>();

	public ObservableCollection<BOQGroupItem> HVACDuctGroups { get; set; } = new ObservableCollection<BOQGroupItem>();

	public ObservableCollection<BOQFilterItem> HVACDuctFittingSystems { get; set; } = new ObservableCollection<BOQFilterItem>();

	public ObservableCollection<BOQGroupItem> HVACDuctFittingGroups { get; set; } = new ObservableCollection<BOQGroupItem>();

	public ObservableCollection<BOQFilterItem> HVACGasSystems { get; set; } = new ObservableCollection<BOQFilterItem>();

	public ObservableCollection<BOQGroupItem> HVACGasGroups { get; set; } = new ObservableCollection<BOQGroupItem>();

	public ObservableCollection<BOQGasSizeItem> GasSizes { get; set; } = new ObservableCollection<BOQGasSizeItem>();

	public ObservableCollection<DuctThicknessRule> DuctThicknessRect { get; set; } = new ObservableCollection<DuctThicknessRule>();

	public ObservableCollection<DuctThicknessRule> DuctThicknessRound { get; set; } = new ObservableCollection<DuctThicknessRule>();

	public ObservableCollection<BOQFilterItem> PCCCSystems { get; set; } = new ObservableCollection<BOQFilterItem>();

	public ObservableCollection<BOQGroupItem> PCCCGroups { get; set; } = new ObservableCollection<BOQGroupItem>();

	public BOQWindow(Document doc)
	{
		InitializeComponent();
		Title = "BIM TOOL – BOQ";
		_doc = doc;
		StaticHVACGasConduitTypes = HVACGasConduitTypes;
		DuctThicknessRect.CollectionChanged += DuctThicknessRect_CollectionChanged;
		DuctThicknessRound.CollectionChanged += DuctThicknessRound_CollectionChanged;
		LoadCategories();
		LoadRevitData();
		LoadSavedSettings();
		UpdateCableTrayFilterSource();
		UpdateConduitFilterSource();
		base.DataContext = this;
		base.Closed += BOQWindow_Closed;
	}

	private void DuctThicknessRect_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.NewItems != null)
		{
			foreach (DuctThicknessRule item in e.NewItems)
			{
				item.PropertyChanged += Rule_PropertyChanged;
			}
		}
		RecalculateRanges();
	}

	private void DuctThicknessRound_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.NewItems != null)
		{
			foreach (DuctThicknessRule item in e.NewItems)
			{
				item.PropertyChanged += Rule_PropertyChanged;
			}
		}
		RecalculateRanges();
	}

	private void Rule_PropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "Limit")
		{
			RecalculateRanges();
			SaveSavedSettings();
		}
	}

	private void RecalculateRanges()
	{
		if (DuctThicknessRect != null)
		{
			double prevLimit = 0.0;
			for (int i = 0; i < DuctThicknessRect.Count; i++)
			{
				DuctThicknessRect[i].LowerLimit = prevLimit + 1.0;
				prevLimit = DuctThicknessRect[i].Limit;
			}
		}
		if (DuctThicknessRound != null)
		{
			double prevLimit2 = 0.0;
			for (int j = 0; j < DuctThicknessRound.Count; j++)
			{
				DuctThicknessRound[j].LowerLimit = prevLimit2 + 1.0;
				prevLimit2 = DuctThicknessRound[j].Limit;
			}
		}
	}

	private void BOQWindow_Closed(object sender, EventArgs e)
	{
		SaveSavedSettings();
	}

	private void Window_TextBoxTextChanged(object sender, TextChangedEventArgs e)
	{
		SaveSavedSettings();
	}

	private void Window_ButtonBaseClick(object sender, RoutedEventArgs e)
	{
		if (e.OriginalSource is CheckBox || e.OriginalSource is RadioButton)
		{
			SaveSavedSettings();
		}
	}

	private void InitializeDefaults()
	{
		DienCableTraySystems.Add(new BOQFilterItem
		{
			FilterType = "contain"
		});
		DienCableTraySystems[0].FilterValues.Add(new BOQFilterValue());
		DienCableTrayGroups.Add(new BOQGroupItem
		{
			GroupName = "Heavy Duty Cable Tray",
			FilterType = "equal"
		});
		DienCableTrayGroups[0].FilterValues.Add(new BOQFilterValue
		{
			Value = "LV"
		});
		DienCableTrayGroups.Add(new BOQGroupItem
		{
			GroupName = "Light Duty Cable Tray",
			FilterType = "equal"
		});
		DienCableTrayGroups[1].FilterValues.Add(new BOQFilterValue
		{
			Value = "ELV"
		});
		DienBuswaySystems.Add(new BOQFilterItem
		{
			FilterType = "equal"
		});
		DienBuswaySystems[0].FilterValues.Add(new BOQFilterValue());
		DienConduitSystems.Add(new BOQFilterItem
		{
			FilterType = "contain"
		});
		DienConduitSystems[0].FilterValues.Add(new BOQFilterValue());
		DienConduitGroups.Add(new BOQGroupItem
		{
			GroupName = "Conduit Group 1",
			FilterType = "contain"
		});
		DienConduitGroups[0].FilterValues.Add(new BOQFilterValue());
		CapThoatNuocSystems.Add(new BOQFilterItem
		{
			FilterType = "contain"
		});
		CapThoatNuocSystems[0].FilterValues.Add(new BOQFilterValue());
		CapThoatNuocGroups.Add(new BOQGroupItem
		{
			GroupName = "Chilled Water Pipe",
			FilterType = "contain"
		});
		CapThoatNuocGroups[0].FilterValues.Add(new BOQFilterValue());
		CapNuocGroups.Add(new BOQGroupItem
		{
			GroupName = "Fire Sprinkler",
			FilterType = "contain"
		});
		CapNuocGroups[0].FilterValues.Add(new BOQFilterValue());
		CapNuocGroups.Add(new BOQGroupItem
		{
			GroupName = "Fire Hose",
			FilterType = "contain"
		});
		CapNuocGroups[1].FilterValues.Add(new BOQFilterValue());
		ThoatNuocGroups.Add(new BOQGroupItem
		{
			GroupName = "Fire Sprinkler",
			FilterType = "contain"
		});
		ThoatNuocGroups[0].FilterValues.Add(new BOQFilterValue());
		ThoatNuocGroups.Add(new BOQGroupItem
		{
			GroupName = "Fire Hose",
			FilterType = "contain"
		});
		ThoatNuocGroups[1].FilterValues.Add(new BOQFilterValue());
		HVACPipeSystems.Add(new BOQFilterItem
		{
			FilterType = "contain"
		});
		HVACPipeSystems[0].FilterValues.Add(new BOQFilterValue());
		HVACDuctSystems.Add(new BOQFilterItem
		{
			FilterType = "contain"
		});
		HVACDuctSystems[0].FilterValues.Add(new BOQFilterValue());
		HVACDuctFittingSystems.Add(new BOQFilterItem
		{
			FilterType = "contain"
		});
		HVACDuctFittingSystems[0].FilterValues.Add(new BOQFilterValue());
		HVACDuctGroups.Add(new BOQGroupItem
		{
			GroupName = "HVAC Duct",
			FilterType = "contain"
		});
		HVACDuctGroups[HVACDuctGroups.Count - 1].FilterValues.Add(new BOQFilterValue());
		HVACDuctFittingGroups.Add(new BOQGroupItem
		{
			GroupName = "HVAC Duct Fitting",
			FilterType = "contain"
		});
		HVACDuctFittingGroups[HVACDuctFittingGroups.Count - 1].FilterValues.Add(new BOQFilterValue());
		HVACPipeGroups.Add(new BOQGroupItem
		{
			GroupName = "HVAC Chilled Water",
			FilterType = "contain"
		});
		HVACPipeGroups[HVACPipeGroups.Count - 1].FilterValues.Add(new BOQFilterValue());
		HVACGasSystems.Add(new BOQFilterItem
		{
			FilterType = "contain"
		});
		HVACGasSystems[0].FilterValues.Add(new BOQFilterValue());
		HVACGasGroups.Add(new BOQGroupItem
		{
			GroupName = "HVAC Refrigerant",
			FilterType = "contain"
		});
		HVACGasGroups[HVACGasGroups.Count - 1].FilterValues.Add(new BOQFilterValue());
		PCCCSystems.Add(new BOQFilterItem
		{
			FilterType = "contain"
		});
		PCCCSystems[0].FilterValues.Add(new BOQFilterValue());
		PCCCGroups.Add(new BOQGroupItem
		{
			GroupName = "Fire Sprinkler",
			FilterType = "contain"
		});
		PCCCGroups[0].FilterValues.Add(new BOQFilterValue());
		PCCCGroups.Add(new BOQGroupItem
		{
			GroupName = "Fire Hose",
			FilterType = "contain"
		});
		PCCCGroups[1].FilterValues.Add(new BOQFilterValue());
		DuctThicknessRect.Add(new DuctThicknessRule
		{
			Limit = 200.0,
			Thickness = 0.58
		});
		DuctThicknessRect.Add(new DuctThicknessRule
		{
			Limit = 350.0,
			Thickness = 0.58
		});
		DuctThicknessRect.Add(new DuctThicknessRule
		{
			Limit = 500.0,
			Thickness = 0.75
		});
		DuctThicknessRect.Add(new DuctThicknessRule
		{
			Limit = 750.0,
			Thickness = 0.75
		});
		DuctThicknessRect.Add(new DuctThicknessRule
		{
			Limit = 1000.0,
			Thickness = 0.75
		});
		DuctThicknessRect.Add(new DuctThicknessRule
		{
			Limit = 1250.0,
			Thickness = 0.95
		});
		DuctThicknessRect.Add(new DuctThicknessRule
		{
			Limit = 1600.0,
			Thickness = 0.95
		});
		DuctThicknessRect.Add(new DuctThicknessRule
		{
			Limit = 2100.0,
			Thickness = 1.15
		});
		DuctThicknessRect.Add(new DuctThicknessRule
		{
			Limit = 2500.0,
			Thickness = 1.15
		});
		DuctThicknessRect.Add(new DuctThicknessRule
		{
			Limit = 99999.0,
			Thickness = 1.15
		});
		DuctThicknessRound.Add(new DuctThicknessRule
		{
			Limit = 200.0,
			Thickness = 0.58
		});
		DuctThicknessRound.Add(new DuctThicknessRule
		{
			Limit = 350.0,
			Thickness = 0.75
		});
		DuctThicknessRound.Add(new DuctThicknessRule
		{
			Limit = 500.0,
			Thickness = 0.75
		});
		DuctThicknessRound.Add(new DuctThicknessRule
		{
			Limit = 750.0,
			Thickness = 0.95
		});
		DuctThicknessRound.Add(new DuctThicknessRule
		{
			Limit = 1000.0,
			Thickness = 0.95
		});
		DuctThicknessRound.Add(new DuctThicknessRule
		{
			Limit = 1250.0,
			Thickness = 1.15
		});
		DuctThicknessRound.Add(new DuctThicknessRule
		{
			Limit = 1600.0,
			Thickness = 1.15
		});
		DuctThicknessRound.Add(new DuctThicknessRule
		{
			Limit = 2100.0,
			Thickness = 1.15
		});
		DuctThicknessRound.Add(new DuctThicknessRule
		{
			Limit = 2500.0,
			Thickness = 1.15
		});
		DuctThicknessRound.Add(new DuctThicknessRule
		{
			Limit = 99999.0,
			Thickness = 1.15
		});
	}

	private void SaveSavedSettings()
	{
		try
		{
			BOQSettings settings = new BOQSettings
			{
				CTSystems = DienCableTraySystems.ToList(),
				CTGroups = DienCableTrayGroups.ToList(),
				BWSystems = DienBuswaySystems.ToList(),
				BWExportMode = ((RbBuswayOption1.IsChecked == true) ? 1 : 2),
				BWGroups = DienBuswayGroups.ToList(),
				CondSystems = DienConduitSystems.ToList(),
				CondGroups = DienConduitGroups.ToList(),
				PipeSystems = CapThoatNuocSystems.Concat(HVACPipeSystems).Concat(PCCCSystems).ToList(),
				PipeGroups = CapThoatNuocGroups.Concat(CapNuocGroups).Concat(ThoatNuocGroups).Concat(PCCCGroups)
					.ToList(),
				DuctSystems = HVACDuctSystems.ToList(),
				CapThoatNuocSystems = CapThoatNuocSystems.ToList(),
				CapThoatNuocGroups = CapThoatNuocGroups.ToList(),
				CapNuocGroups = CapNuocGroups.ToList(),
				ThoatNuocGroups = ThoatNuocGroups.ToList(),
				HVACPipeSystems = HVACPipeSystems.ToList(),
				HVACPipeGroups = HVACPipeGroups.ToList(),
				HVACDuctSystems = HVACDuctSystems.ToList(),
				HVACDuctGroups = HVACDuctGroups.ToList(),
				HVACDuctFittingSystems = HVACDuctSystems.ToList(),
				HVACDuctFittingGroups = HVACDuctGroups.ToList(),
				HVACGasSystems = HVACGasSystems.ToList(),
				HVACGasGroups = HVACGasGroups.ToList(),
				PCCCSystems = PCCCSystems.ToList(),
				PCCCGroups = PCCCGroups.ToList(),
				DuctThicknessRect = DuctThicknessRect.ToList(),
				DuctThicknessRound = DuctThicknessRound.ToList(),
				CTCutMode = ((RbServiceType.IsChecked == true) ? "ServiceType" : "CableTrayType"),
				CondCutMode = ((RbConduitServiceType.IsChecked == true) ? "ServiceType" : "ConduitType"),
				ExportAll = (ChkSysAll.IsChecked == true),
				ExportDien = (ChkSysDien.IsChecked == true),
				ExportDienCableTray = (ChkDienCableTray.IsChecked == true),
				ExportDienBusway = (ChkDienBusway.IsChecked == true),
				ExportDienConduit = (ChkDienConduit.IsChecked == true),
				ExportNuoc = (ChkSysNuoc.IsChecked == true),
				ExportNuocCap = (ChkNuocCap.IsChecked == true),
				ExportNuocThoat = (ChkNuocThoat.IsChecked == true),
				ExportHvac = (ChkSysHvac.IsChecked == true),
				ExportHvacDuct = (ChkHvacDuct.IsChecked == true),
				ExportHvacDuctFitting = false,
				ExportHvacPipe = (ChkHvacPipe.IsChecked == true),
				ExportHvacGas = (ChkHvacGas.IsChecked == true),
				HvacDuctAggregate = (RbHvacDuctAggregate.IsChecked == true),
				HvacDuctExportMode = ((RbHvacDuctAggregate.IsChecked == true) ? 1 : ((RbHvacDuctNoAggregate.IsChecked == true) ? 2 : 3)),
				HvacDuctFittingAggregate = false,
				GasSizes = GasSizes.ToList(),
				HvacGasExportMode = 3,
				ExportPccc = (ChkSysPccc.IsChecked == true),
				ExportPcccPipe = (ChkPcccPipe.IsChecked == true),
				SelectedOnly = false,
				InViewOnly = (RbInView.IsChecked == true),
				AutoOpen = (CheckAutoOpen.IsChecked == true),
				IncludeFileLink = (CheckIncludeFileLink.IsChecked == true),
				ExportPath = TxtSavePath.Text,
				SelectedCategoryNames = ((from c in Categories?.Where((BOQCategoryItem c) => c.IsSelected)
					select c.Name).ToList() ?? new List<string>())
			};
			string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
			string dir = System.IO.Path.GetDirectoryName(SettingsFilePath);
			if (!Directory.Exists(dir))
			{
				Directory.CreateDirectory(dir);
			}
			File.WriteAllText(SettingsFilePath, json);
		}
		catch
		{
		}
	}

	private void LoadSavedSettings()
	{
		try
		{
			if (File.Exists(SettingsFilePath))
			{
				string json = File.ReadAllText(SettingsFilePath);
				BOQSettings settings = JsonConvert.DeserializeObject<BOQSettings>(json);
				if (settings != null)
				{
					if (settings.CTSystems != null && settings.CTSystems.Any())
					{
						DienCableTraySystems.Clear();
						foreach (BOQFilterItem item in settings.CTSystems)
						{
							DienCableTraySystems.Add(item);
						}
					}
					if (settings.CTGroups != null && settings.CTGroups.Any())
					{
						DienCableTrayGroups.Clear();
						foreach (BOQGroupItem item2 in settings.CTGroups)
						{
							DienCableTrayGroups.Add(item2);
						}
					}
					if (settings.BWSystems != null && settings.BWSystems.Any())
					{
						DienBuswaySystems.Clear();
						foreach (BOQFilterItem item3 in settings.BWSystems)
						{
							DienBuswaySystems.Add(item3);
						}
					}
					if (settings.BWExportMode == 1)
					{
						RbBuswayOption1.IsChecked = true;
						RbBuswayOption2.IsChecked = false;
					}
					else
					{
						RbBuswayOption1.IsChecked = false;
						RbBuswayOption2.IsChecked = true;
					}
					if (settings.CondSystems != null && settings.CondSystems.Any())
					{
						DienConduitSystems.Clear();
						foreach (BOQFilterItem item4 in settings.CondSystems)
						{
							DienConduitSystems.Add(item4);
						}
					}
					if (settings.CondGroups != null && settings.CondGroups.Any())
					{
						DienConduitGroups.Clear();
						foreach (BOQGroupItem item5 in settings.CondGroups)
						{
							DienConduitGroups.Add(item5);
						}
					}
					if (settings.CapThoatNuocSystems != null && settings.CapThoatNuocSystems.Any())
					{
						CapThoatNuocSystems.Clear();
						foreach (BOQFilterItem item6 in settings.CapThoatNuocSystems)
						{
							CapThoatNuocSystems.Add(item6);
						}
					}
					if (settings.CapThoatNuocGroups != null && settings.CapThoatNuocGroups.Any())
					{
						CapThoatNuocGroups.Clear();
						foreach (BOQGroupItem item7 in settings.CapThoatNuocGroups)
						{
							CapThoatNuocGroups.Add(item7);
						}
					}
					if (settings.CapNuocGroups != null && settings.CapNuocGroups.Any())
					{
						CapNuocGroups.Clear();
						foreach (BOQGroupItem item8 in settings.CapNuocGroups)
						{
							CapNuocGroups.Add(item8);
						}
					}
					if (settings.ThoatNuocGroups != null && settings.ThoatNuocGroups.Any())
					{
						ThoatNuocGroups.Clear();
						foreach (BOQGroupItem item9 in settings.ThoatNuocGroups)
						{
							ThoatNuocGroups.Add(item9);
						}
					}
					if (settings.HVACPipeSystems != null && settings.HVACPipeSystems.Any())
					{
						HVACPipeSystems.Clear();
						foreach (BOQFilterItem item10 in settings.HVACPipeSystems)
						{
							HVACPipeSystems.Add(item10);
						}
					}
					if (settings.HVACPipeGroups != null && settings.HVACPipeGroups.Any())
					{
						HVACPipeGroups.Clear();
						foreach (BOQGroupItem item11 in settings.HVACPipeGroups)
						{
							HVACPipeGroups.Add(item11);
						}
					}
					if (settings.HVACDuctSystems != null && settings.HVACDuctSystems.Any())
					{
						HVACDuctSystems.Clear();
						foreach (BOQFilterItem item12 in settings.HVACDuctSystems)
						{
							HVACDuctSystems.Add(item12);
						}
					}
					if (settings.HVACDuctGroups != null && settings.HVACDuctGroups.Any())
					{
						HVACDuctGroups.Clear();
						foreach (BOQGroupItem item13 in settings.HVACDuctGroups)
						{
							HVACDuctGroups.Add(item13);
						}
					}
					if (settings.HVACDuctFittingSystems != null && settings.HVACDuctFittingSystems.Any())
					{
						HVACDuctFittingSystems.Clear();
						foreach (BOQFilterItem item14 in settings.HVACDuctFittingSystems)
						{
							HVACDuctFittingSystems.Add(item14);
						}
					}
					if (settings.HVACDuctFittingGroups != null && settings.HVACDuctFittingGroups.Any())
					{
						HVACDuctFittingGroups.Clear();
						foreach (BOQGroupItem item15 in settings.HVACDuctFittingGroups)
						{
							HVACDuctFittingGroups.Add(item15);
						}
					}
					if (settings.HVACGasSystems != null && settings.HVACGasSystems.Any())
					{
						HVACGasSystems.Clear();
						foreach (BOQFilterItem item16 in settings.HVACGasSystems)
						{
							HVACGasSystems.Add(item16);
						}
					}
					if (settings.HVACGasGroups != null && settings.HVACGasGroups.Any())
					{
						HVACGasGroups.Clear();
						foreach (BOQGroupItem item17 in settings.HVACGasGroups)
						{
							HVACGasGroups.Add(item17);
						}
					}
					if (settings.GasSizes != null)
					{
						foreach (BOQGasSizeItem sizeItem in GasSizes)
						{
							BOQGasSizeItem saved = settings.GasSizes.FirstOrDefault((BOQGasSizeItem s) => s.Name == sizeItem.Name);
							if (saved != null)
							{
								sizeItem.IsChecked = saved.IsChecked;
							}
						}
					}
					if (settings.PCCCSystems != null && settings.PCCCSystems.Any())
					{
						PCCCSystems.Clear();
						foreach (BOQFilterItem item18 in settings.PCCCSystems)
						{
							PCCCSystems.Add(item18);
						}
					}
					if (settings.PCCCGroups != null && settings.PCCCGroups.Any())
					{
						PCCCGroups.Clear();
						foreach (BOQGroupItem item19 in settings.PCCCGroups)
						{
							PCCCGroups.Add(item19);
						}
					}
					if (settings.DuctThicknessRect != null && settings.DuctThicknessRect.Any())
					{
						DuctThicknessRect.Clear();
						foreach (DuctThicknessRule item20 in settings.DuctThicknessRect)
						{
							if (item20.Thickness > 1.15)
							{
								item20.Thickness = 1.15;
							}
							DuctThicknessRect.Add(item20);
						}
					}
					if (settings.DuctThicknessRound != null && settings.DuctThicknessRound.Any())
					{
						DuctThicknessRound.Clear();
						foreach (DuctThicknessRule item21 in settings.DuctThicknessRound)
						{
							if (item21.Thickness > 1.15)
							{
								item21.Thickness = 1.15;
							}
							DuctThicknessRound.Add(item21);
						}
					}
					ChkSysAll.IsChecked = settings.ExportAll;
					ChkSysDien.IsChecked = settings.ExportDien;
					ChkDienCableTray.IsChecked = settings.ExportDienCableTray;
					ChkDienBusway.IsChecked = settings.ExportDienBusway;
					ChkDienConduit.IsChecked = settings.ExportDienConduit;
					ChkSysNuoc.IsChecked = settings.ExportNuoc;
					ChkNuocCap.IsChecked = settings.ExportNuocCap;
					ChkNuocThoat.IsChecked = settings.ExportNuocThoat;
					ChkSysHvac.IsChecked = settings.ExportHvac;
					ChkHvacDuct.IsChecked = settings.ExportHvacDuct;
					ChkHvacPipe.IsChecked = settings.ExportHvacPipe;
					ChkHvacGas.IsChecked = settings.ExportHvacGas;
					int hvacMode = settings.HvacDuctExportMode;
					if (hvacMode == 1 && !settings.HvacDuctAggregate)
					{
						hvacMode = 2;
					}
					switch (hvacMode)
					{
					case 1:
						RbHvacDuctAggregate.IsChecked = true;
						break;
					case 2:
						RbHvacDuctNoAggregate.IsChecked = true;
						break;
					case 3:
						RbHvacDuctOpt3.IsChecked = true;
						break;
					}
					ChkSysPccc.IsChecked = settings.ExportPccc;
					ChkPcccPipe.IsChecked = settings.ExportPcccPipe;
					RbInView.IsChecked = settings.InViewOnly;
					RbAll.IsChecked = !settings.InViewOnly;
					CheckAutoOpen.IsChecked = settings.AutoOpen;
					CheckIncludeFileLink.IsChecked = settings.IncludeFileLink;
					TxtSavePath.Text = settings.ExportPath;
					if (settings.CTCutMode == "ServiceType")
					{
						RbServiceType.IsChecked = true;
						RbCableTrayType.IsChecked = false;
					}
					else
					{
						RbServiceType.IsChecked = false;
						RbCableTrayType.IsChecked = true;
					}
					if (settings.CondCutMode == "ServiceType")
					{
						RbConduitServiceType.IsChecked = true;
						RbConduitType.IsChecked = false;
					}
					else
					{
						RbConduitServiceType.IsChecked = false;
						RbConduitType.IsChecked = true;
					}
					if (settings.SelectedCategoryNames == null || Categories == null)
					{
						return;
					}
					{
						foreach (BOQCategoryItem cat in Categories)
						{
							cat.IsSelected = settings.SelectedCategoryNames.Contains(cat.Name);
						}
						return;
					}
				}
			}
		}
		catch
		{
		}
		InitializeDefaults();
	}

	private void BtnAddFilter_Click(object sender, RoutedEventArgs e)
	{
		FrameworkElement btn = sender as FrameworkElement;
		if (btn.Tag is ObservableCollection<BOQFilterItem> collection)
		{
			collection.Add(new BOQFilterItem());
		}
	}

	private void BtnRemoveFilter_Click(object sender, RoutedEventArgs e)
	{
		FrameworkElement btn = sender as FrameworkElement;
		if (btn.DataContext is BOQFilterItem item)
		{
			ItemsControl itemsControl = FindParent<ItemsControl>(btn);
			if (itemsControl != null && itemsControl.ItemsSource is ObservableCollection<BOQFilterItem> collection)
			{
				collection.Remove(item);
			}
		}
	}

	private void BtnRemoveGroup_Click(object sender, RoutedEventArgs e)
	{
		FrameworkElement btn = sender as FrameworkElement;
		if (btn.DataContext is BOQGroupItem item)
		{
			ItemsControl itemsControl = FindParent<ItemsControl>(btn);
			if (itemsControl != null && itemsControl.ItemsSource is ObservableCollection<BOQGroupItem> collection)
			{
				collection.Remove(item);
			}
		}
	}

	private void BtnAddGroup_Click(object sender, RoutedEventArgs e)
	{
		FrameworkElement btn = sender as FrameworkElement;
		if (btn.Tag is ObservableCollection<BOQGroupItem> collection)
		{
			collection.Add(new BOQGroupItem
			{
				GroupName = "New Group"
			});
		}
	}

	private void BtnCheckAllGasSizes_Click(object sender, RoutedEventArgs e)
	{
		foreach (BOQGasSizeItem size in GasSizes)
		{
			size.IsChecked = true;
		}
	}

	private void BtnUncheckAllGasSizes_Click(object sender, RoutedEventArgs e)
	{
		foreach (BOQGasSizeItem size in GasSizes)
		{
			size.IsChecked = false;
		}
	}

	private void BtnAddFilterRow_Click(object sender, RoutedEventArgs e)
	{
		FrameworkElement btn = sender as FrameworkElement;
		ItemsControl itemsControl = FindParent<ItemsControl>(btn);
		if (itemsControl != null && itemsControl.ItemsSource is ObservableCollection<BOQFilterItem> collection)
		{
			BOQFilterItem newItem = new BOQFilterItem();
			newItem.FilterValues.Add(new BOQFilterValue());
			collection.Add(newItem);
		}
	}

	private void BtnAddValue_Click(object sender, RoutedEventArgs e)
	{
		FrameworkElement btn = sender as FrameworkElement;
		if (btn.DataContext is BOQFilterItem filter)
		{
			filter.FilterValues.Add(new BOQFilterValue());
		}
		else if (btn.DataContext is BOQGroupItem group)
		{
			group.FilterValues.Add(new BOQFilterValue());
		}
	}

	private void BtnRemoveValue_Click(object sender, RoutedEventArgs e)
	{
		FrameworkElement btn = sender as FrameworkElement;
		if (!(btn.DataContext is BOQFilterValue val))
		{
			return;
		}
		ItemsControl itemsControl = FindParent<ItemsControl>(btn);
		if (itemsControl != null)
		{
			ObservableCollection<BOQFilterValue> collection = itemsControl.ItemsSource as ObservableCollection<BOQFilterValue>;
			if (collection != null && collection.Count > 1)
			{
				collection.Remove(val);
			}
			else if (collection != null && collection.Count == 1)
			{
				val.Value = "";
			}
		}
	}

	private void BtnAddGroupRow_Click(object sender, RoutedEventArgs e)
	{
		FrameworkElement btn = sender as FrameworkElement;
		ItemsControl itemsControl = FindParent<ItemsControl>(btn);
		if (itemsControl != null && itemsControl.ItemsSource is ObservableCollection<BOQGroupItem> collection)
		{
			BOQGroupItem newItem = new BOQGroupItem
			{
				GroupName = "New Group"
			};
			newItem.FilterValues.Add(new BOQFilterValue());
			collection.Add(newItem);
		}
	}

	private T FindParent<T>(DependencyObject child) where T : DependencyObject
	{
		DependencyObject parentObject = VisualTreeHelper.GetParent(child);
		if (parentObject == null)
		{
			return null;
		}
		if (parentObject is T parent)
		{
			return parent;
		}
		return FindParent<T>(parentObject);
	}

	private void LoadRevitData()
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_04a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0183: Unknown result type (might be due to invalid IL or missing references)
		//IL_054f: Unknown result type (might be due to invalid IL or missing references)
		//IL_029d: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			List<string> ctTypes = (from ElementType x in (IEnumerable)new FilteredElementCollector(_doc).OfClass(typeof(CableTrayType))
				select ((Element)x).Name into x
				orderby x
				select x).ToList();
			foreach (string name in ctTypes)
			{
				CableTrayTypes.Add(name);
			}
			List<string> serviceTypes = (from x in (from val in ((IEnumerable)new FilteredElementCollector(_doc).OfCategory((BuiltInCategory)(-2008130)).WhereElementIsNotElementType()).Cast<Element>().Select(delegate(Element e)
					{
						Parameter obj = e.get_Parameter((BuiltInParameter)(-1140128));
						return (obj != null) ? obj.AsString() : null;
					})
					where !string.IsNullOrEmpty(val)
					select val).Distinct()
				orderby x
				select x).ToList();
			foreach (string name2 in serviceTypes)
			{
				CableTrayServiceTypes.Add(name2);
			}
			List<string> condTypes = (from ElementType x in (IEnumerable)new FilteredElementCollector(_doc).OfClass(typeof(ConduitType))
				select ((Element)x).Name into x
				orderby x
				select x).ToList();
			HashSet<string> uniqueGasSizes = new HashSet<string>();
			foreach (string name3 in condTypes)
			{
				ConduitTypes.Add(name3);
				if (!name3.StartsWith("ø", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				HVACGasConduitTypes.Add(name3);
				List<string> sizes = BOQExporter.ExtractGasSizes(name3);
				foreach (string s in sizes)
				{
					uniqueGasSizes.Add(s);
				}
			}
			try
			{
				List<string> conduitServiceTypes = (from x in (from val in ((IEnumerable)new FilteredElementCollector(_doc).OfCategory((BuiltInCategory)(-2008132)).WhereElementIsNotElementType()).Cast<Element>().Select(delegate(Element e)
						{
							//IL_003e: Unknown result type (might be due to invalid IL or missing references)
							//IL_0044: Invalid comparison between Unknown and I4
							Parameter val2 = e.get_Parameter((BuiltInParameter)(-1140128)) ?? e.LookupParameter("Service Type") ?? e.LookupParameter("ServiceType");
							return (val2 == null || !val2.HasValue) ? "" : (((int)val2.StorageType == 3) ? val2.AsString() : val2.AsValueString());
						})
						where !string.IsNullOrEmpty(val)
						select val).Distinct()
					orderby x
					select x).ToList();
				foreach (string name4 in conduitServiceTypes)
				{
					ConduitServiceTypes.Add(name4);
				}
			}
			catch
			{
			}
			GasSizes.Clear();
			double result;
			foreach (string s2 in uniqueGasSizes.OrderBy((string x) => double.TryParse(x, NumberStyles.Any, CultureInfo.InvariantCulture, out result) ? result : 9999.0))
			{
				GasSizes.Add(new BOQGasSizeItem
				{
					Name = s2,
					IsChecked = true
				});
			}
			List<string> pTypes = (from ElementType x in (IEnumerable)new FilteredElementCollector(_doc).OfClass(typeof(PipeType))
				select ((Element)x).Name into x
				orderby x
				select x).ToList();
			foreach (string name5 in pTypes)
			{
				PipeTypes.Add(name5);
			}
			List<string> dTypes = (from ElementType x in (IEnumerable)new FilteredElementCollector(_doc).OfClass(typeof(DuctType))
				select ((Element)x).Name into x
				orderby x
				select x).ToList();
			foreach (string name6 in dTypes)
			{
				DuctTypes.Add(name6);
			}
			List<MEPSystemType> allSysTypes = ((IEnumerable)new FilteredElementCollector(_doc).OfClass(typeof(MEPSystemType))).Cast<MEPSystemType>().ToList();
			foreach (MEPSystemType st in allSysTypes)
			{
				if (st is PipingSystemType)
				{
					PipeSystemTypes.Add(((Element)st).Name);
					continue;
				}
				if (false)
				{
					DuctSystemTypes.Add(((Element)st).Name);
					continue;
				}
				PipeSystemTypes.Add(((Element)st).Name);
				DuctSystemTypes.Add(((Element)st).Name);
			}
			List<string> pList = (from x in PipeSystemTypes.Distinct()
				orderby x
				select x).ToList();
			PipeSystemTypes.Clear();
			foreach (string n in pList)
			{
				PipeSystemTypes.Add(n);
			}
			List<string> dList = (from x in DuctSystemTypes.Distinct()
				orderby x
				select x).ToList();
			DuctSystemTypes.Clear();
			foreach (string n2 in dList)
			{
				DuctSystemTypes.Add(n2);
			}
			if (DuctSystemTypes.Count != 0)
			{
				return;
			}
			foreach (MEPSystemType st2 in allSysTypes)
			{
				DuctSystemTypes.Add(((Element)st2).Name);
			}
		}
		catch
		{
		}
	}

	private void LoadCategories()
	{
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		Categories = new ObservableCollection<BOQCategoryItem>();
		try
		{
			IOrderedEnumerable<Category> categories = from Category c in (IEnumerable)_doc.Settings.Categories
				where (int)c.CategoryType == 1
				orderby c.Name
				select c;
			foreach (Category cat in categories)
			{
				if (new FilteredElementCollector(_doc).OfCategoryId(cat.Id).WhereElementIsNotElementType().FirstElement() != null)
				{
					Categories.Add(new BOQCategoryItem
					{
						Name = cat.Name,
						Category = cat,
						IsSelected = false
					});
				}
			}
		}
		catch
		{
		}
	}

	private void CmbCongTac_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (CmbCongTac == null)
		{
			return;
		}
		FrameworkElement pDien = PanelDien ?? (FindName("PanelDien") as FrameworkElement);
		FrameworkElement pNuoc = PanelCapThoatNuoc ?? (FindName("PanelCapThoatNuoc") as FrameworkElement);
		FrameworkElement pHvac = PanelHVAC ?? (FindName("PanelHVAC") as FrameworkElement);
		FrameworkElement pPccc = PanelChuaChay ?? (FindName("PanelChuaChay") as FrameworkElement);
		if (pDien != null && pNuoc != null && pHvac != null && pPccc != null)
		{
			pDien.Visibility = System.Windows.Visibility.Collapsed;
			pNuoc.Visibility = System.Windows.Visibility.Collapsed;
			pHvac.Visibility = System.Windows.Visibility.Collapsed;
			pPccc.Visibility = System.Windows.Visibility.Collapsed;
			switch (CmbCongTac.SelectedIndex)
			{
			case 0:
				pDien.Visibility = System.Windows.Visibility.Visible;
				break;
			case 1:
				pNuoc.Visibility = System.Windows.Visibility.Visible;
				break;
			case 2:
				pHvac.Visibility = System.Windows.Visibility.Visible;
				break;
			case 3:
				pPccc.Visibility = System.Windows.Visibility.Visible;
				break;
			}
		}
	}

	private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (e.ClickCount == 2)
		{
			base.WindowState = ((base.WindowState != WindowState.Maximized) ? WindowState.Maximized : WindowState.Normal);
		}
		else if (e.LeftButton == MouseButtonState.Pressed)
		{
			DragMove();
		}
	}

	private void BtnMinimize_Click(object sender, RoutedEventArgs e)
	{
		base.WindowState = WindowState.Minimized;
	}

	private void BtnMaximize_Click(object sender, RoutedEventArgs e)
	{
		base.WindowState = ((base.WindowState != WindowState.Maximized) ? WindowState.Maximized : WindowState.Normal);
	}

	private void BtnClose_Click(object sender, RoutedEventArgs e)
	{
		Close();
	}

	private void Window_StateChanged(object sender, EventArgs e)
	{
		if (PathMaximize != null && PathRestore != null)
		{
			if (base.WindowState == WindowState.Maximized)
			{
				PathMaximize.Visibility = System.Windows.Visibility.Collapsed;
				PathRestore.Visibility = System.Windows.Visibility.Visible;
			}
			else
			{
				PathMaximize.Visibility = System.Windows.Visibility.Visible;
				PathRestore.Visibility = System.Windows.Visibility.Collapsed;
			}
		}
	}

	private void Window_KeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Escape)
		{
			Close();
		}
	}

	private void BtnBrowse_Click(object sender, RoutedEventArgs e)
	{
		SaveFileDialog sfd = new SaveFileDialog
		{
			Filter = "Excel Files (*.xlsx)|*.xlsx",
			FileName = "BOQ_Report_" + DateTime.Now.ToString("yyyyMMdd")
		};
		if (sfd.ShowDialog() == true)
		{
			TxtSavePath.Text = sfd.FileName;
		}
	}

	private void BtnExport_Click(object sender, RoutedEventArgs e)
	{
		if (string.IsNullOrEmpty(TxtSavePath.Text))
		{
			MessageBox.Show("Please select a save file path!", "Warning", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		List<BOQCategoryItem> categoriesToExport = Categories.ToList();
		if (!categoriesToExport.Any())
		{
			MessageBox.Show("No elements found to export!", "Information", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}
		try
		{
			bool selectedOnly = false;
			bool inViewOnly = RbInView.IsChecked == true;
			string sysMode = "All";
			bool isDienChecked = ChkSysDien.IsChecked == true || !ChkSysDien.IsChecked.HasValue;
			bool isNuocChecked = ChkSysNuoc.IsChecked == true || !ChkSysNuoc.IsChecked.HasValue;
			bool isHvacChecked = ChkSysHvac.IsChecked == true || !ChkSysHvac.IsChecked.HasValue;
			bool isPcccChecked = ChkSysPccc.IsChecked == true || !ChkSysPccc.IsChecked.HasValue;
			sysMode = ((isDienChecked && !isNuocChecked && !isHvacChecked && !isPcccChecked) ? "Dien" : ((!isDienChecked && isNuocChecked && !isHvacChecked && !isPcccChecked) ? "Nuoc" : ((!isDienChecked && !isNuocChecked && isHvacChecked && !isPcccChecked) ? "Hvac" : ((!(!isDienChecked && !isNuocChecked && !isHvacChecked && isPcccChecked)) ? "All" : "Pccc"))));
			BOQSettings settings = new BOQSettings
			{
				CTCutMode = ((RbCableTrayType.IsChecked == true) ? "CableTrayType" : "ServiceType"),
				CTSystems = DienCableTraySystems.Select((BOQFilterItem x) => new BOQFilterItem
				{
					FilterType = x.FilterType,
					FilterValues = new ObservableCollection<BOQFilterValue>(x.FilterValues.Select((BOQFilterValue v) => new BOQFilterValue
					{
						Value = v.Value
					}))
				}).ToList(),
				CTGroups = DienCableTrayGroups.Select((BOQGroupItem x) => new BOQGroupItem
				{
					GroupName = x.GroupName,
					FilterType = x.FilterType,
					FilterValues = new ObservableCollection<BOQFilterValue>(x.FilterValues.Select((BOQFilterValue v) => new BOQFilterValue
					{
						Value = v.Value
					}))
				}).ToList(),
				BWSystems = DienBuswaySystems.Select((BOQFilterItem x) => new BOQFilterItem
				{
					FilterType = x.FilterType,
					FilterValues = new ObservableCollection<BOQFilterValue>(x.FilterValues.Select((BOQFilterValue v) => new BOQFilterValue
					{
						Value = v.Value
					}))
				}).ToList(),
				BWExportMode = ((RbBuswayOption1.IsChecked == true) ? 1 : 2),
				BWGroups = DienBuswayGroups.Select((BOQGroupItem x) => new BOQGroupItem
				{
					GroupName = x.GroupName,
					FilterType = x.FilterType,
					FilterValues = new ObservableCollection<BOQFilterValue>(x.FilterValues.Select((BOQFilterValue v) => new BOQFilterValue
					{
						Value = v.Value
					}))
				}).ToList(),
				CondSystems = DienConduitSystems.Select((BOQFilterItem x) => new BOQFilterItem
				{
					FilterType = x.FilterType,
					FilterValues = new ObservableCollection<BOQFilterValue>(x.FilterValues.Select((BOQFilterValue v) => new BOQFilterValue
					{
						Value = v.Value
					}))
				}).ToList(),
				CondGroups = DienConduitGroups.Select((BOQGroupItem x) => new BOQGroupItem
				{
					GroupName = x.GroupName,
					FilterType = x.FilterType,
					FilterValues = new ObservableCollection<BOQFilterValue>(x.FilterValues.Select((BOQFilterValue v) => new BOQFilterValue
					{
						Value = v.Value
					}))
				}).ToList(),
				CondCutMode = ((RbConduitServiceType.IsChecked == true) ? "ServiceType" : "ConduitType"),
				CapThoatNuocSystems = CapThoatNuocSystems.Select((BOQFilterItem x) => new BOQFilterItem
				{
					FilterType = x.FilterType,
					FilterValues = new ObservableCollection<BOQFilterValue>(x.FilterValues.Select((BOQFilterValue v) => new BOQFilterValue
					{
						Value = v.Value
					}))
				}).ToList(),
				CapThoatNuocGroups = CapThoatNuocGroups.Select((BOQGroupItem x) => new BOQGroupItem
				{
					GroupName = x.GroupName,
					FilterType = x.FilterType,
					FilterValues = new ObservableCollection<BOQFilterValue>(x.FilterValues.Select((BOQFilterValue v) => new BOQFilterValue
					{
						Value = v.Value
					}))
				}).ToList(),
				CapNuocGroups = CapNuocGroups.Select((BOQGroupItem x) => new BOQGroupItem
				{
					GroupName = x.GroupName,
					FilterType = x.FilterType,
					FilterValues = new ObservableCollection<BOQFilterValue>(x.FilterValues.Select((BOQFilterValue v) => new BOQFilterValue
					{
						Value = v.Value
					}))
				}).ToList(),
				ThoatNuocGroups = ThoatNuocGroups.Select((BOQGroupItem x) => new BOQGroupItem
				{
					GroupName = x.GroupName,
					FilterType = x.FilterType,
					FilterValues = new ObservableCollection<BOQFilterValue>(x.FilterValues.Select((BOQFilterValue v) => new BOQFilterValue
					{
						Value = v.Value
					}))
				}).ToList(),
				HVACPipeSystems = HVACPipeSystems.Select((BOQFilterItem x) => new BOQFilterItem
				{
					FilterType = x.FilterType,
					FilterValues = new ObservableCollection<BOQFilterValue>(x.FilterValues.Select((BOQFilterValue v) => new BOQFilterValue
					{
						Value = v.Value
					}))
				}).ToList(),
				HVACDuctSystems = HVACDuctSystems.Select((BOQFilterItem x) => new BOQFilterItem
				{
					FilterType = x.FilterType,
					FilterValues = new ObservableCollection<BOQFilterValue>(x.FilterValues.Select((BOQFilterValue v) => new BOQFilterValue
					{
						Value = v.Value
					}))
				}).ToList(),
				PCCCSystems = PCCCSystems.Select((BOQFilterItem x) => new BOQFilterItem
				{
					FilterType = x.FilterType,
					FilterValues = new ObservableCollection<BOQFilterValue>(x.FilterValues.Select((BOQFilterValue v) => new BOQFilterValue
					{
						Value = v.Value
					}))
				}).ToList(),
				PCCCGroups = PCCCGroups.Select((BOQGroupItem x) => new BOQGroupItem
				{
					GroupName = x.GroupName,
					FilterType = x.FilterType,
					FilterValues = new ObservableCollection<BOQFilterValue>(x.FilterValues.Select((BOQFilterValue v) => new BOQFilterValue
					{
						Value = v.Value
					}))
				}).ToList(),
				HVACPipeGroups = HVACPipeGroups.Select((BOQGroupItem x) => new BOQGroupItem
				{
					GroupName = x.GroupName,
					FilterType = x.FilterType,
					FilterValues = new ObservableCollection<BOQFilterValue>(x.FilterValues.Select((BOQFilterValue v) => new BOQFilterValue
					{
						Value = v.Value
					}))
				}).ToList(),
				HVACDuctGroups = HVACDuctGroups.Select((BOQGroupItem x) => new BOQGroupItem
				{
					GroupName = x.GroupName,
					FilterType = x.FilterType,
					FilterValues = new ObservableCollection<BOQFilterValue>(x.FilterValues.Select((BOQFilterValue v) => new BOQFilterValue
					{
						Value = v.Value
					}))
				}).ToList(),
				HVACDuctFittingGroups = HVACDuctGroups.Select((BOQGroupItem x) => new BOQGroupItem
				{
					GroupName = x.GroupName,
					FilterType = x.FilterType,
					FilterValues = new ObservableCollection<BOQFilterValue>(x.FilterValues.Select((BOQFilterValue v) => new BOQFilterValue
					{
						Value = v.Value
					}))
				}).ToList(),
				HVACGasGroups = HVACGasGroups.Select((BOQGroupItem x) => new BOQGroupItem
				{
					GroupName = x.GroupName,
					FilterType = x.FilterType,
					FilterValues = new ObservableCollection<BOQFilterValue>(x.FilterValues.Select((BOQFilterValue v) => new BOQFilterValue
					{
						Value = v.Value
					}))
				}).ToList(),
				PipeSystems = (from x in CapThoatNuocSystems.Concat(HVACPipeSystems).Concat(PCCCSystems)
					select new BOQFilterItem
					{
						FilterType = x.FilterType,
						FilterValues = new ObservableCollection<BOQFilterValue>(x.FilterValues.Select((BOQFilterValue v) => new BOQFilterValue
						{
							Value = v.Value
						}))
					}).ToList(),
				PipeGroups = (from x in CapThoatNuocGroups.Concat(CapNuocGroups).Concat(ThoatNuocGroups).Concat(HVACPipeGroups)
						.Concat(PCCCGroups)
					select new BOQGroupItem
					{
						GroupName = x.GroupName,
						FilterType = x.FilterType,
						FilterValues = new ObservableCollection<BOQFilterValue>(x.FilterValues.Select((BOQFilterValue v) => new BOQFilterValue
						{
							Value = v.Value
						}))
					}).ToList(),
				DuctSystems = HVACDuctSystems.Select((BOQFilterItem x) => new BOQFilterItem
				{
					FilterType = x.FilterType,
					FilterValues = new ObservableCollection<BOQFilterValue>(x.FilterValues.Select((BOQFilterValue v) => new BOQFilterValue
					{
						Value = v.Value
					}))
				}).ToList(),
				HVACDuctFittingSystems = HVACDuctSystems.Select((BOQFilterItem x) => new BOQFilterItem
				{
					FilterType = x.FilterType,
					FilterValues = new ObservableCollection<BOQFilterValue>(x.FilterValues.Select((BOQFilterValue v) => new BOQFilterValue
					{
						Value = v.Value
					}))
				}).ToList(),
				ExportAll = (ChkSysAll.IsChecked == true),
				ExportDien = (ChkSysDien.IsChecked == true),
				ExportDienCableTray = (ChkDienCableTray.IsChecked == true),
				ExportDienBusway = (ChkDienBusway.IsChecked == true),
				ExportDienConduit = (ChkDienConduit.IsChecked == true),
				ExportNuoc = (ChkSysNuoc.IsChecked == true),
				ExportNuocCap = (ChkNuocCap.IsChecked == true),
				ExportNuocThoat = (ChkNuocThoat.IsChecked == true),
				ExportHvac = (ChkSysHvac.IsChecked == true),
				ExportHvacDuct = (ChkHvacDuct.IsChecked == true),
				ExportHvacDuctFitting = false,
				ExportHvacPipe = (ChkHvacPipe.IsChecked == true),
				ExportHvacGas = (ChkHvacGas.IsChecked == true),
				HvacDuctAggregate = (RbHvacDuctAggregate.IsChecked == true),
				HvacDuctExportMode = ((RbHvacDuctAggregate.IsChecked == true) ? 1 : ((RbHvacDuctNoAggregate.IsChecked == true) ? 2 : 3)),
				HvacDuctFittingAggregate = false,
				ExportPccc = (ChkSysPccc.IsChecked == true),
				ExportPcccPipe = (ChkPcccPipe.IsChecked == true),
				DuctThicknessRect = DuctThicknessRect.ToList(),
				DuctThicknessRound = DuctThicknessRound.ToList(),
				IncludeFileLink = (CheckIncludeFileLink.IsChecked == true)
			};
			try
			{
				string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
				string dir = System.IO.Path.GetDirectoryName(SettingsFilePath);
				if (!Directory.Exists(dir))
				{
					Directory.CreateDirectory(dir);
				}
				File.WriteAllText(SettingsFilePath, json);
			}
			catch
			{
			}
			List<(string, string, Action<BOQSettings>)> systemsToExport = new List<(string, string, Action<BOQSettings>)>();
			bool hasDien = settings.ExportDienCableTray || settings.ExportDienBusway || settings.ExportDienConduit;
			bool hasNuoc = settings.ExportNuocCap || settings.ExportNuocThoat;
			bool hasHvac = settings.ExportHvacDuct || settings.ExportHvacPipe || settings.ExportHvacGas;
			bool hasPccc = settings.ExportPcccPipe;
			if (hasDien)
			{
				systemsToExport.Add(("Dien", "Dien", delegate(BOQSettings s)
				{
					s.ExportDien = true;
					s.ExportNuoc = false;
					s.ExportNuocCap = false;
					s.ExportNuocThoat = false;
					s.ExportHvac = false;
					s.ExportHvacDuct = false;
					s.ExportHvacPipe = false;
					s.ExportHvacGas = false;
					s.ExportPccc = false;
					s.ExportPcccPipe = false;
					s.ExportAll = false;
				}));
			}
			if (hasNuoc)
			{
				systemsToExport.Add(("Nuoc", "Nuoc", delegate(BOQSettings s)
				{
					s.ExportNuoc = true;
					s.ExportDien = false;
					s.ExportDienCableTray = false;
					s.ExportDienBusway = false;
					s.ExportDienConduit = false;
					s.ExportHvac = false;
					s.ExportHvacDuct = false;
					s.ExportHvacPipe = false;
					s.ExportHvacGas = false;
					s.ExportPccc = false;
					s.ExportPcccPipe = false;
					s.ExportAll = false;
				}));
			}
			if (hasHvac)
			{
				systemsToExport.Add(("Hvac", "Hvac", delegate(BOQSettings s)
				{
					s.ExportHvac = true;
					s.ExportDien = false;
					s.ExportDienCableTray = false;
					s.ExportDienBusway = false;
					s.ExportDienConduit = false;
					s.ExportNuoc = false;
					s.ExportNuocCap = false;
					s.ExportNuocThoat = false;
					s.ExportPccc = false;
					s.ExportPcccPipe = false;
					s.ExportAll = false;
				}));
			}
			if (hasPccc)
			{
				systemsToExport.Add(("Pccc", "Pccc", delegate(BOQSettings s)
				{
					s.ExportPccc = true;
					s.ExportPcccPipe = true;
					s.ExportDien = false;
					s.ExportDienCableTray = false;
					s.ExportDienBusway = false;
					s.ExportDienConduit = false;
					s.ExportNuoc = false;
					s.ExportNuocCap = false;
					s.ExportNuocThoat = false;
					s.ExportHvac = false;
					s.ExportHvacDuct = false;
					s.ExportHvacPipe = false;
					s.ExportHvacGas = false;
					s.ExportAll = false;
				}));
			}
			string basePath = TxtSavePath.Text;
			if (basePath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
			{
				basePath = basePath.Substring(0, basePath.Length - 5);
			}
			int total = categoriesToExport.Count;
			int systemCount = ((systemsToExport.Count == 0) ? 1 : systemsToExport.Count);
			MainProgressBar.Maximum = total * systemCount;
			MainProgressBar.Value = 0.0;
			TxtStatus.Text = $"Preparing export... (0/{total})";
			DoEvents();
			bool isMulti = systemsToExport.Count > 1;
			string lastFilePath = TxtSavePath.Text;
			if (!isMulti)
			{
				BOQExporter.Export(_doc, categoriesToExport, TxtSavePath.Text, selectedOnly, inViewOnly, CheckAutoOpen.IsChecked == true, settings, sysMode, delegate(int processed, int tot)
				{
					MainProgressBar.Value = processed;
					TxtStatus.Text = $"Exporting quantities... ({processed}/{tot})";
					DoEvents();
				});
				lastFilePath = TxtSavePath.Text;
			}
			else
			{
				int fileIdx = 0;
				foreach (var item in systemsToExport)
				{
					string subSysMode = item.Item1;
					string suffix = item.Item2;
					Action<BOQSettings> applySettings = item.Item3;
					BOQSettings singleSettings = JsonConvert.DeserializeObject<BOQSettings>(JsonConvert.SerializeObject(settings));
					applySettings(singleSettings);
					string outPath = basePath + "_" + suffix + ".xlsx";
					lastFilePath = outPath;
					int baseProgress = fileIdx * total;
					TxtStatus.Text = $"Exporting {suffix}... (0/{total})";
					DoEvents();
					BOQExporter.Export(_doc, categoriesToExport, outPath, selectedOnly, inViewOnly, autoOpen: false, singleSettings, subSysMode, delegate(int processed, int tot)
					{
						MainProgressBar.Value = baseProgress + processed;
						TxtStatus.Text = $"Exporting {suffix}... ({processed}/{tot})";
						DoEvents();
					});
					fileIdx++;
				}
				if (CheckAutoOpen.IsChecked == true && !string.IsNullOrEmpty(lastFilePath))
				{
					Process.Start(new ProcessStartInfo(lastFilePath)
					{
						UseShellExecute = true
					});
				}
			}
			TxtStatus.Text = "Completed!";
			MainProgressBar.Value = MainProgressBar.Maximum;
			DoEvents();
			string doneMsg = (isMulti ? $"Exported {systemsToExport.Count} files to:\n{System.IO.Path.GetDirectoryName(basePath)}" : "Export completed successfully!");
			MessageBox.Show(doneMsg, "Information", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			if (CheckAutoOpen.IsChecked == false)
			{
				Close();
			}
		}
		catch (Exception ex)
		{
			TxtStatus.Text = "Export error!";
			MessageBox.Show("Export error: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void DoEvents()
	{
		Application wpfApp = Application.Current;
		if (wpfApp != null && wpfApp.Dispatcher != null)
		{
			wpfApp.Dispatcher.Invoke(DispatcherPriority.Background, (Action)delegate
			{
			});
		}
	}

	private void RbCableTrayType_Checked(object sender, RoutedEventArgs e)
	{
		UpdateCableTrayFilterSource();
	}

	private void RbServiceType_Checked(object sender, RoutedEventArgs e)
	{
		UpdateCableTrayFilterSource();
	}

	public void UpdateCableTrayFilterSource()
	{
		if (CableTrayFilterSource == null)
		{
			return;
		}
		CableTrayFilterSource.Clear();
		if (RbCableTrayType != null && RbCableTrayType.IsChecked == true)
		{
			foreach (string item in CableTrayTypes)
			{
				CableTrayFilterSource.Add(item);
			}
			return;
		}
		foreach (string item2 in CableTrayServiceTypes)
		{
			CableTrayFilterSource.Add(item2);
		}
	}

	private void RbConduitType_Checked(object sender, RoutedEventArgs e)
	{
		UpdateConduitFilterSource();
	}

	private void RbConduitServiceType_Checked(object sender, RoutedEventArgs e)
	{
		UpdateConduitFilterSource();
	}

	public void UpdateConduitFilterSource()
	{
		if (ConduitFilterSource == null)
		{
			return;
		}
		ConduitFilterSource.Clear();
		if (RbConduitType != null && RbConduitType.IsChecked == true)
		{
			foreach (string item in ConduitTypes)
			{
				ConduitFilterSource.Add(item);
			}
			return;
		}
		foreach (string item2 in ConduitServiceTypes)
		{
			ConduitFilterSource.Add(item2);
		}
	}

	private void ChkSysAll_Checked(object sender, RoutedEventArgs e)
	{
		if (_isUpdatingCheckboxes || ChkSysDien == null || ChkSysNuoc == null || ChkSysHvac == null || ChkSysPccc == null)
		{
			return;
		}
		_isUpdatingCheckboxes = true;
		try
		{
			ChkSysDien.IsChecked = true;
			ChkSysNuoc.IsChecked = true;
			ChkSysHvac.IsChecked = true;
			ChkSysPccc.IsChecked = true;
			if (ChkDienCableTray != null)
			{
				ChkDienCableTray.IsChecked = true;
			}
			if (ChkDienBusway != null)
			{
				ChkDienBusway.IsChecked = true;
			}
			if (ChkDienConduit != null)
			{
				ChkDienConduit.IsChecked = true;
			}
			if (ChkNuocCap != null)
			{
				ChkNuocCap.IsChecked = true;
			}
			if (ChkNuocThoat != null)
			{
				ChkNuocThoat.IsChecked = true;
			}
			if (ChkHvacDuct != null)
			{
				ChkHvacDuct.IsChecked = true;
			}
			if (ChkHvacPipe != null)
			{
				ChkHvacPipe.IsChecked = true;
			}
			if (ChkHvacGas != null)
			{
				ChkHvacGas.IsChecked = true;
			}
			if (ChkPcccPipe != null)
			{
				ChkPcccPipe.IsChecked = true;
			}
		}
		finally
		{
			_isUpdatingCheckboxes = false;
		}
	}

	private void ChkSysAll_Unchecked(object sender, RoutedEventArgs e)
	{
		if (_isUpdatingCheckboxes || ChkSysDien == null || ChkSysNuoc == null || ChkSysHvac == null || ChkSysPccc == null)
		{
			return;
		}
		_isUpdatingCheckboxes = true;
		try
		{
			ChkSysDien.IsChecked = false;
			ChkSysNuoc.IsChecked = false;
			ChkSysHvac.IsChecked = false;
			ChkSysPccc.IsChecked = false;
			if (ChkDienCableTray != null)
			{
				ChkDienCableTray.IsChecked = false;
			}
			if (ChkDienBusway != null)
			{
				ChkDienBusway.IsChecked = false;
			}
			if (ChkDienConduit != null)
			{
				ChkDienConduit.IsChecked = false;
			}
			if (ChkNuocCap != null)
			{
				ChkNuocCap.IsChecked = false;
			}
			if (ChkNuocThoat != null)
			{
				ChkNuocThoat.IsChecked = false;
			}
			if (ChkHvacDuct != null)
			{
				ChkHvacDuct.IsChecked = false;
			}
			if (ChkHvacPipe != null)
			{
				ChkHvacPipe.IsChecked = false;
			}
			if (ChkHvacGas != null)
			{
				ChkHvacGas.IsChecked = false;
			}
			if (ChkPcccPipe != null)
			{
				ChkPcccPipe.IsChecked = false;
			}
		}
		finally
		{
			_isUpdatingCheckboxes = false;
		}
	}

	private void ChkSysDien_Checked(object sender, RoutedEventArgs e)
	{
		if (_isUpdatingCheckboxes || ChkDienCableTray == null || ChkDienBusway == null || ChkDienConduit == null)
		{
			return;
		}
		_isUpdatingCheckboxes = true;
		try
		{
			ChkDienCableTray.IsChecked = true;
			ChkDienBusway.IsChecked = true;
			ChkDienConduit.IsChecked = true;
			UpdateParentCheckboxes();
		}
		finally
		{
			_isUpdatingCheckboxes = false;
		}
	}

	private void ChkSysDien_Unchecked(object sender, RoutedEventArgs e)
	{
		if (_isUpdatingCheckboxes || ChkDienCableTray == null || ChkDienBusway == null || ChkDienConduit == null)
		{
			return;
		}
		_isUpdatingCheckboxes = true;
		try
		{
			ChkDienCableTray.IsChecked = false;
			ChkDienBusway.IsChecked = false;
			ChkDienConduit.IsChecked = false;
			UpdateParentCheckboxes();
		}
		finally
		{
			_isUpdatingCheckboxes = false;
		}
	}

	private void ChkSysNuoc_Checked(object sender, RoutedEventArgs e)
	{
		if (_isUpdatingCheckboxes || ChkNuocCap == null || ChkNuocThoat == null)
		{
			return;
		}
		_isUpdatingCheckboxes = true;
		try
		{
			ChkNuocCap.IsChecked = true;
			ChkNuocThoat.IsChecked = true;
			UpdateParentCheckboxes();
		}
		finally
		{
			_isUpdatingCheckboxes = false;
		}
	}

	private void ChkSysNuoc_Unchecked(object sender, RoutedEventArgs e)
	{
		if (_isUpdatingCheckboxes || ChkNuocCap == null || ChkNuocThoat == null)
		{
			return;
		}
		_isUpdatingCheckboxes = true;
		try
		{
			ChkNuocCap.IsChecked = false;
			ChkNuocThoat.IsChecked = false;
			UpdateParentCheckboxes();
		}
		finally
		{
			_isUpdatingCheckboxes = false;
		}
	}

	private void ChkSysHvac_Checked(object sender, RoutedEventArgs e)
	{
		if (_isUpdatingCheckboxes || ChkHvacDuct == null || ChkHvacPipe == null || ChkHvacGas == null)
		{
			return;
		}
		_isUpdatingCheckboxes = true;
		try
		{
			ChkHvacDuct.IsChecked = true;
			ChkHvacPipe.IsChecked = true;
			ChkHvacGas.IsChecked = true;
			UpdateParentCheckboxes();
		}
		finally
		{
			_isUpdatingCheckboxes = false;
		}
	}

	private void ChkSysHvac_Unchecked(object sender, RoutedEventArgs e)
	{
		if (_isUpdatingCheckboxes || ChkHvacDuct == null || ChkHvacPipe == null || ChkHvacGas == null)
		{
			return;
		}
		_isUpdatingCheckboxes = true;
		try
		{
			ChkHvacDuct.IsChecked = false;
			ChkHvacPipe.IsChecked = false;
			ChkHvacGas.IsChecked = false;
			UpdateParentCheckboxes();
		}
		finally
		{
			_isUpdatingCheckboxes = false;
		}
	}

	private void ChkSysPccc_Checked(object sender, RoutedEventArgs e)
	{
		if (_isUpdatingCheckboxes || ChkPcccPipe == null)
		{
			return;
		}
		_isUpdatingCheckboxes = true;
		try
		{
			ChkPcccPipe.IsChecked = true;
			UpdateParentCheckboxes();
		}
		finally
		{
			_isUpdatingCheckboxes = false;
		}
	}

	private void ChkSysPccc_Unchecked(object sender, RoutedEventArgs e)
	{
		if (_isUpdatingCheckboxes || ChkPcccPipe == null)
		{
			return;
		}
		_isUpdatingCheckboxes = true;
		try
		{
			ChkPcccPipe.IsChecked = false;
			UpdateParentCheckboxes();
		}
		finally
		{
			_isUpdatingCheckboxes = false;
		}
	}

	private void RbHvacDuctAggregate_Checked(object sender, RoutedEventArgs e)
	{
		SaveSavedSettings();
	}

	private void RbHvacDuctNoAggregate_Checked(object sender, RoutedEventArgs e)
	{
		SaveSavedSettings();
	}

	private void RbHvacDuctOpt3_Checked(object sender, RoutedEventArgs e)
	{
		SaveSavedSettings();
	}

	private void ChkSubSys_Checked(object sender, RoutedEventArgs e)
	{
		if (_isUpdatingCheckboxes)
		{
			return;
		}
		_isUpdatingCheckboxes = true;
		try
		{
			UpdateParentCheckboxes();
		}
		finally
		{
			_isUpdatingCheckboxes = false;
		}
	}

	private void ChkSubSys_Unchecked(object sender, RoutedEventArgs e)
	{
		if (_isUpdatingCheckboxes)
		{
			return;
		}
		_isUpdatingCheckboxes = true;
		try
		{
			UpdateParentCheckboxes();
		}
		finally
		{
			_isUpdatingCheckboxes = false;
		}
	}

	private void UpdateParentCheckboxes()
	{
		if (ChkDienCableTray != null && ChkDienBusway != null && ChkDienConduit != null && ChkSysDien != null && ChkNuocCap != null && ChkNuocThoat != null && ChkSysNuoc != null && ChkHvacDuct != null && ChkHvacPipe != null && ChkHvacGas != null && ChkSysHvac != null && ChkPcccPipe != null && ChkSysPccc != null && ChkSysAll != null)
		{
			bool anyDien = ChkDienCableTray.IsChecked == true || ChkDienBusway.IsChecked == true || ChkDienConduit.IsChecked == true;
			bool allDien = ChkDienCableTray.IsChecked == true && ChkDienBusway.IsChecked == true && ChkDienConduit.IsChecked == true;
			ChkSysDien.IsChecked = (allDien ? new bool?(true) : (anyDien ? ((bool?)null) : new bool?(false)));
			bool anyNuoc = ChkNuocCap.IsChecked == true || ChkNuocThoat.IsChecked == true;
			bool allNuoc = ChkNuocCap.IsChecked == true && ChkNuocThoat.IsChecked == true;
			ChkSysNuoc.IsChecked = (allNuoc ? new bool?(true) : (anyNuoc ? ((bool?)null) : new bool?(false)));
			bool anyHvac = ChkHvacDuct.IsChecked == true || ChkHvacPipe.IsChecked == true || ChkHvacGas.IsChecked == true;
			bool allHvac = ChkHvacDuct.IsChecked == true && ChkHvacPipe.IsChecked == true && ChkHvacGas.IsChecked == true;
			ChkSysHvac.IsChecked = (allHvac ? new bool?(true) : (anyHvac ? ((bool?)null) : new bool?(false)));
			bool anyPccc = ChkPcccPipe.IsChecked == true;
			bool allPccc = ChkPcccPipe.IsChecked == true;
			ChkSysPccc.IsChecked = (allPccc ? new bool?(true) : (anyPccc ? ((bool?)null) : new bool?(false)));
			bool anyAll = ChkSysDien.IsChecked != false || ChkSysNuoc.IsChecked != false || ChkSysHvac.IsChecked != false || ChkSysPccc.IsChecked != false;
			bool allAll = ChkSysDien.IsChecked == true && ChkSysNuoc.IsChecked == true && ChkSysHvac.IsChecked == true && ChkSysPccc.IsChecked == true;
			ChkSysAll.IsChecked = (allAll ? new bool?(true) : (anyAll ? ((bool?)null) : new bool?(false)));
		}
	}

	private void BtnAddDuctThicknessRect_Click(object sender, RoutedEventArgs e)
	{
		DuctThicknessRect.Add(new DuctThicknessRule());
		SaveSavedSettings();
	}

	private void BtnRemoveDuctThicknessRect_Click(object sender, RoutedEventArgs e)
	{
		if (DuctThicknessRect.Any())
		{
			DuctThicknessRect.RemoveAt(DuctThicknessRect.Count - 1);
		}
		SaveSavedSettings();
	}

	private void BtnAddDuctThicknessRound_Click(object sender, RoutedEventArgs e)
	{
		DuctThicknessRound.Add(new DuctThicknessRule());
		SaveSavedSettings();
	}

	private void BtnRemoveDuctThicknessRound_Click(object sender, RoutedEventArgs e)
	{
		if (DuctThicknessRound.Any())
		{
			DuctThicknessRound.RemoveAt(DuctThicknessRound.Count - 1);
		}
		SaveSavedSettings();
	}

	private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
	{
		base.Dispatcher.BeginInvoke((Action)delegate
		{
			SaveSavedSettings();
		}, DispatcherPriority.Background);
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.27.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocater = new Uri("/BIN;component/43.%20boq/boqwindow.xaml", UriKind.Relative);
			global::BIN.EmbeddedBamlLoader.LoadComponent(this, resourceLocater);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.27.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 1:
			((BOQWindow)target).StateChanged += Window_StateChanged;
			((BOQWindow)target).KeyDown += Window_KeyDown;
			((BOQWindow)target).AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(Window_TextBoxTextChanged));
			((BOQWindow)target).AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(Window_ButtonBaseClick));
			break;
		case 2:
			((Border)target).MouseLeftButtonDown += Border_MouseLeftButtonDown;
			break;
		case 3:
			((Button)target).Click += BtnMinimize_Click;
			break;
		case 4:
			BtnMaximize = (Button)target;
			BtnMaximize.Click += BtnMaximize_Click;
			break;
		case 5:
			PathMaximize = (System.Windows.Shapes.Path)target;
			break;
		case 6:
			PathRestore = (System.Windows.Shapes.Path)target;
			break;
		case 7:
			((Button)target).Click += BtnClose_Click;
			break;
		case 8:
			RbAll = (RadioButton)target;
			break;
		case 9:
			RbInView = (RadioButton)target;
			break;
		case 10:
			CmbCongTac = (ComboBox)target;
			CmbCongTac.SelectionChanged += CmbCongTac_SelectionChanged;
			break;
		case 19:
			PanelDien = (StackPanel)target;
			break;
		case 20:
			RbServiceType = (RadioButton)target;
			RbServiceType.Checked += RbServiceType_Checked;
			break;
		case 21:
			RbCableTrayType = (RadioButton)target;
			RbCableTrayType.Checked += RbCableTrayType_Checked;
			break;
		case 22:
			((Button)target).Click += BtnAddGroup_Click;
			break;
		case 23:
			RbBuswayOption1 = (RadioButton)target;
			break;
		case 24:
			((Button)target).Click += BtnAddFilter_Click;
			break;
		case 25:
			RbBuswayOption2 = (RadioButton)target;
			break;
		case 26:
			((Button)target).Click += BtnAddFilter_Click;
			break;
		case 27:
			RbConduitServiceType = (RadioButton)target;
			RbConduitServiceType.Checked += RbConduitServiceType_Checked;
			break;
		case 28:
			RbConduitType = (RadioButton)target;
			RbConduitType.Checked += RbConduitType_Checked;
			break;
		case 29:
			((Button)target).Click += BtnAddGroup_Click;
			break;
		case 30:
			PanelCapThoatNuoc = (StackPanel)target;
			break;
		case 31:
			((Button)target).Click += BtnAddGroup_Click;
			break;
		case 32:
			((Button)target).Click += BtnAddGroup_Click;
			break;
		case 33:
			PanelHVAC = (StackPanel)target;
			break;
		case 34:
			RbHvacDuctAggregate = (RadioButton)target;
			RbHvacDuctAggregate.Checked += RbHvacDuctAggregate_Checked;
			break;
		case 35:
			RbHvacDuctNoAggregate = (RadioButton)target;
			RbHvacDuctNoAggregate.Checked += RbHvacDuctNoAggregate_Checked;
			break;
		case 36:
			RbHvacDuctOpt3 = (RadioButton)target;
			RbHvacDuctOpt3.Checked += RbHvacDuctOpt3_Checked;
			break;
		case 37:
			((Button)target).Click += BtnAddGroup_Click;
			break;
		case 38:
			((Button)target).Click += BtnAddGroup_Click;
			break;
		case 39:
			((Button)target).Click += BtnCheckAllGasSizes_Click;
			break;
		case 40:
			((Button)target).Click += BtnUncheckAllGasSizes_Click;
			break;
		case 41:
			PanelChuaChay = (StackPanel)target;
			break;
		case 42:
			((Button)target).Click += BtnAddGroup_Click;
			break;
		case 43:
			((DataGrid)target).CellEditEnding += DataGrid_CellEditEnding;
			break;
		case 44:
			((Button)target).Click += BtnAddDuctThicknessRect_Click;
			break;
		case 45:
			((Button)target).Click += BtnRemoveDuctThicknessRect_Click;
			break;
		case 46:
			((DataGrid)target).CellEditEnding += DataGrid_CellEditEnding;
			break;
		case 47:
			((Button)target).Click += BtnAddDuctThicknessRound_Click;
			break;
		case 48:
			((Button)target).Click += BtnRemoveDuctThicknessRound_Click;
			break;
		case 49:
			ChkSysAll = (CheckBox)target;
			ChkSysAll.Checked += ChkSysAll_Checked;
			ChkSysAll.Unchecked += ChkSysAll_Unchecked;
			break;
		case 50:
			ChkSysDien = (CheckBox)target;
			ChkSysDien.Checked += ChkSysDien_Checked;
			ChkSysDien.Unchecked += ChkSysDien_Unchecked;
			break;
		case 51:
			ChkDienCableTray = (CheckBox)target;
			ChkDienCableTray.Checked += ChkSubSys_Checked;
			ChkDienCableTray.Unchecked += ChkSubSys_Unchecked;
			break;
		case 52:
			ChkDienBusway = (CheckBox)target;
			ChkDienBusway.Checked += ChkSubSys_Checked;
			ChkDienBusway.Unchecked += ChkSubSys_Unchecked;
			break;
		case 53:
			ChkDienConduit = (CheckBox)target;
			ChkDienConduit.Checked += ChkSubSys_Checked;
			ChkDienConduit.Unchecked += ChkSubSys_Unchecked;
			break;
		case 54:
			ChkSysNuoc = (CheckBox)target;
			ChkSysNuoc.Checked += ChkSysNuoc_Checked;
			ChkSysNuoc.Unchecked += ChkSysNuoc_Unchecked;
			break;
		case 55:
			ChkNuocCap = (CheckBox)target;
			ChkNuocCap.Checked += ChkSubSys_Checked;
			ChkNuocCap.Unchecked += ChkSubSys_Unchecked;
			break;
		case 56:
			ChkNuocThoat = (CheckBox)target;
			ChkNuocThoat.Checked += ChkSubSys_Checked;
			ChkNuocThoat.Unchecked += ChkSubSys_Unchecked;
			break;
		case 57:
			ChkSysHvac = (CheckBox)target;
			ChkSysHvac.Checked += ChkSysHvac_Checked;
			ChkSysHvac.Unchecked += ChkSysHvac_Unchecked;
			break;
		case 58:
			ChkHvacDuct = (CheckBox)target;
			ChkHvacDuct.Checked += ChkSubSys_Checked;
			ChkHvacDuct.Unchecked += ChkSubSys_Unchecked;
			break;
		case 59:
			ChkHvacPipe = (CheckBox)target;
			ChkHvacPipe.Checked += ChkSubSys_Checked;
			ChkHvacPipe.Unchecked += ChkSubSys_Unchecked;
			break;
		case 60:
			ChkHvacGas = (CheckBox)target;
			ChkHvacGas.Checked += ChkSubSys_Checked;
			ChkHvacGas.Unchecked += ChkSubSys_Unchecked;
			break;
		case 61:
			ChkSysPccc = (CheckBox)target;
			ChkSysPccc.Checked += ChkSysPccc_Checked;
			ChkSysPccc.Unchecked += ChkSysPccc_Unchecked;
			break;
		case 62:
			ChkPcccPipe = (CheckBox)target;
			ChkPcccPipe.Checked += ChkSubSys_Checked;
			ChkPcccPipe.Unchecked += ChkSubSys_Unchecked;
			break;
		case 63:
			TxtStatus = (TextBlock)target;
			break;
		case 64:
			MainProgressBar = (ProgressBar)target;
			break;
		case 65:
			TxtSavePath = (TextBox)target;
			break;
		case 66:
			((Button)target).Click += BtnBrowse_Click;
			break;
		case 67:
			CheckAutoOpen = (CheckBox)target;
			break;
		case 68:
			CheckIncludeFileLink = (CheckBox)target;
			break;
		case 69:
			((Button)target).Click += BtnExport_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.27.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IStyleConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 11:
			((Button)target).Click += BtnRemoveFilter_Click;
			break;
		case 12:
			((Button)target).Click += BtnAddValue_Click;
			break;
		case 13:
			((Button)target).Click += BtnRemoveGroup_Click;
			break;
		case 14:
			((Button)target).Click += BtnRemoveValue_Click;
			break;
		case 15:
			((Button)target).Click += BtnAddValue_Click;
			break;
		case 16:
			((Button)target).Click += BtnRemoveGroup_Click;
			break;
		case 17:
			((Button)target).Click += BtnRemoveValue_Click;
			break;
		case 18:
			((Button)target).Click += BtnAddValue_Click;
			break;
		}
	}
}
