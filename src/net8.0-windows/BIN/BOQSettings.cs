using System.Collections.Generic;

namespace BIN;

public class BOQSettings
{
	public int HvacGasExportMode { get; set; } = 1;

	public List<BOQGasSizeItem> GasSizes { get; set; }

	public List<DuctThicknessRule> DuctThicknessRect { get; set; }

	public List<DuctThicknessRule> DuctThicknessRound { get; set; }

	public List<BOQFilterItem> CTSystems { get; set; }

	public List<BOQGroupItem> CTGroups { get; set; }

	public List<BOQFilterItem> BWSystems { get; set; }

	public int BWExportMode { get; set; } = 1;

	public List<BOQGroupItem> BWGroups { get; set; }

	public List<BOQFilterItem> CondSystems { get; set; }

	public List<BOQGroupItem> CondGroups { get; set; }

	public string CondCutMode { get; set; } = "ServiceType";

	public List<BOQFilterItem> PipeSystems { get; set; }

	public List<BOQGroupItem> PipeGroups { get; set; }

	public List<BOQFilterItem> DuctSystems { get; set; }

	public List<BOQFilterItem> CapThoatNuocSystems { get; set; }

	public List<BOQGroupItem> CapThoatNuocGroups { get; set; }

	public List<BOQGroupItem> CapNuocGroups { get; set; }

	public List<BOQGroupItem> ThoatNuocGroups { get; set; }

	public List<BOQFilterItem> HVACPipeSystems { get; set; }

	public List<BOQGroupItem> HVACPipeGroups { get; set; }

	public List<BOQFilterItem> HVACDuctSystems { get; set; }

	public List<BOQGroupItem> HVACDuctGroups { get; set; }

	public List<BOQFilterItem> HVACDuctFittingSystems { get; set; }

	public List<BOQGroupItem> HVACDuctFittingGroups { get; set; }

	public List<BOQFilterItem> HVACGasSystems { get; set; }

	public List<BOQGroupItem> HVACGasGroups { get; set; }

	public List<BOQFilterItem> PCCCSystems { get; set; }

	public List<BOQGroupItem> PCCCGroups { get; set; }

	public string CTCutMode { get; set; } = "ServiceType";

	public bool ExportAll { get; set; } = true;

	public bool ExportDien { get; set; } = true;

	public bool ExportDienCableTray { get; set; } = true;

	public bool ExportDienBusway { get; set; } = true;

	public bool ExportDienConduit { get; set; } = true;

	public bool ExportNuoc { get; set; } = true;

	public bool ExportNuocPipe { get; set; } = true;

	public bool ExportNuocCap { get; set; } = true;

	public bool ExportNuocThoat { get; set; } = true;

	public bool ExportHvac { get; set; } = true;

	public bool ExportHvacDuct { get; set; } = true;

	public bool ExportHvacDuctFitting { get; set; } = true;

	public bool ExportHvacPipe { get; set; } = true;

	public bool ExportHvacGas { get; set; } = true;

	public bool HvacDuctAggregate { get; set; } = true;

	public int HvacDuctExportMode { get; set; } = 1;

	public bool HvacDuctFittingAggregate { get; set; } = true;

	public bool ExportPccc { get; set; } = true;

	public bool ExportPcccPipe { get; set; } = true;

	public bool SelectedOnly { get; set; }

	public bool InViewOnly { get; set; }

	public bool AutoOpen { get; set; }

	public bool IncludeFileLink { get; set; } = false;

	public string ExportPath { get; set; }

	public List<string> SelectedCategoryNames { get; set; } = new List<string>();
}
