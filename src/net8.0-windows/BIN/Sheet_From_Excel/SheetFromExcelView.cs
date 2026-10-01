using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Markup;
using Autodesk.Revit.DB;
using ExcelDataReader;
using Microsoft.Office.Interop.Excel;

namespace BIN.Sheet_From_Excel;

public class SheetFromExcelView : Window, IComponentConnector
{
	private Document doc;

	internal System.Windows.Controls.TextBox tb_FilePath;

	internal System.Windows.Controls.Button bt_Browse;

	internal System.Windows.Controls.ComboBox cbb_TitleBlocks;

	internal System.Windows.Controls.Button bt_Ok;

	internal System.Windows.Controls.Button bt_Cancel;

	private bool _contentLoaded;

	protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
	{
		base.OnPreviewKeyDown(e);
		if (e.Key == Key.Escape)
		{
			Close();
		}
	}

	public SheetFromExcelView(Document doc)
	{
		InitializeComponent();
		this.doc = doc;
		cbb_TitleBlocks.ItemsSource = GetListTitleBlock();
		cbb_TitleBlocks.SelectedIndex = 0;
	}

	private void bt_Browse_Click(object sender, RoutedEventArgs e)
	{
		OpenFileDialog dialog = new OpenFileDialog();
		dialog.Title = "Select Excel File";
		dialog.Filter = "Excel File | *xls; *xlsx; xlsm";
		if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
		{
			tb_FilePath.Text = dialog.FileName;
		}
		else
		{
			tb_FilePath.Text = "";
		}
	}

	private List<string> GetListTitleBlock()
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		List<FamilySymbol> collector = ((IEnumerable)new FilteredElementCollector(doc).OfCategory((BuiltInCategory)(-2000280)).WhereElementIsElementType()).Cast<FamilySymbol>().ToList();
		List<string> listNames = collector.Select((FamilySymbol x) => ((ElementType)x).FamilyName + " : " + ((Element)x).Name).ToList();
		listNames.Sort();
		return listNames;
	}

	private ElementId GetTitleBlockId(string titleBlockName)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		List<FamilySymbol> collector = ((IEnumerable)new FilteredElementCollector(doc).OfCategory((BuiltInCategory)(-2000280)).WhereElementIsElementType()).Cast<FamilySymbol>().ToList();
		return ((Element)collector.Find((FamilySymbol x) => ((ElementType)x).FamilyName + " : " + ((Element)x).Name == titleBlockName)).Id;
	}

	private void bt_Ok_Click(object sender, RoutedEventArgs e)
	{
		string titleBlockName = cbb_TitleBlocks.SelectedValue as string;
		ElementId titleBlockId = GetTitleBlockId(titleBlockName);
		string filePath = tb_FilePath.Text;
		if (string.IsNullOrEmpty(filePath))
		{
			System.Windows.MessageBox.Show("Select Excel File Or Copy/Paste The Path", "Message");
			return;
		}
		if (filePath.Contains("\""))
		{
			filePath = filePath.Replace("\"", "");
		}
		DataTable excelData = new DataTable();
		using (FileStream fileStream = File.Open(filePath, FileMode.Open, FileAccess.Read))
		{
			using IExcelDataReader reader = ExcelReaderFactory.CreateReader(fileStream);
			DataSet data = reader.AsDataSet();
			if (data != null)
			{
				excelData = data.Tables[0];
			}
		}
		CloseExcelFile(filePath);
		CreateSheet(excelData, titleBlockId);
	}

	private void bt_Cancel_Click(object sender, RoutedEventArgs e)
	{
		Close();
	}

	private void CloseExcelFile(string filePath)
	{
		Microsoft.Office.Interop.Excel.Application app = (Microsoft.Office.Interop.Excel.Application)Activator.CreateInstance(Marshal.GetTypeFromCLSID(new Guid("00024500-0000-0000-C000-000000000046")));
		if (app == null)
		{
			return;
		}
		foreach (Workbook workbook in app.Workbooks)
		{
			if (workbook.FullName == filePath)
			{
				workbook.Close(false, Type.Missing, Type.Missing);
				break;
			}
		}
		Marshal.ReleaseComObject(app);
	}

	private void CreateSheet(DataTable excelData, ElementId blockId)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Expected O, but got Unknown
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		List<ViewSheet> sheetList = new List<ViewSheet>();
		Transaction t = new Transaction(doc, " ");
		try
		{
			t.Start();
			int rowCount = excelData.Rows.Count;
			int columnCount = excelData.Columns.Count;
			for (int i = 1; i < rowCount; i++)
			{
				ViewSheet viewSheet = ViewSheet.Create(doc, blockId);
				sheetList.Add(viewSheet);
				for (int j = 0; j < columnCount; j++)
				{
					string header = excelData.Columns[j].ColumnName;
					string parameterName = excelData.Rows[0][header].ToString();
					string parameterValue = excelData.Rows[i][header].ToString();
					SetParameter(viewSheet, parameterName, parameterValue);
				}
			}
			t.Commit();
		}
		finally
		{
			((IDisposable)t)?.Dispose();
		}
		Close();
		System.Windows.MessageBox.Show($"{sheetList.Count} sheets created", "Message");
	}

	private void SetParameter(ViewSheet viewSheet, string parameterName, string parameterValue)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Invalid comparison between Unknown and I4
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Invalid comparison between Unknown and I4
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Invalid comparison between Unknown and I4
		try
		{
			Parameter para = ((Element)viewSheet).LookupParameter(parameterName);
			if (para != null && !((APIObject)para).IsReadOnly)
			{
				StorageType paraType = para.StorageType;
				if ((int)paraType == 1)
				{
					para.Set(int.Parse(parameterValue));
				}
				else if ((int)paraType == 2)
				{
					para.Set(double.Parse(parameterValue));
				}
				else if ((int)paraType == 3)
				{
					para.Set(parameterValue);
				}
			}
		}
		catch
		{
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.27.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocater = new Uri("/BIN;component/12.%20sheet%20from%20excel/sheetfromexcelview.xaml", UriKind.Relative);
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
			tb_FilePath = (System.Windows.Controls.TextBox)target;
			break;
		case 2:
			bt_Browse = (System.Windows.Controls.Button)target;
			bt_Browse.Click += bt_Browse_Click;
			break;
		case 3:
			cbb_TitleBlocks = (System.Windows.Controls.ComboBox)target;
			break;
		case 4:
			bt_Ok = (System.Windows.Controls.Button)target;
			bt_Ok.Click += bt_Ok_Click;
			break;
		case 5:
			bt_Cancel = (System.Windows.Controls.Button)target;
			bt_Cancel.Click += bt_Cancel_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
