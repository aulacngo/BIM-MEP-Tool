using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.Office.Interop.Excel;

[ComImport]
[CompilerGenerated]
[Guid("000208DA-0000-0000-C000-000000000046")]
[TypeIdentifier]
public interface _Workbook
{
	void _VtblGap1_20();

	[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
	[DispId(277)]
	[LCIDConversion(3)]
	void Close([Optional][In][MarshalAs(UnmanagedType.Struct)] object SaveChanges, [Optional][In][MarshalAs(UnmanagedType.Struct)] object Filename, [Optional][In][MarshalAs(UnmanagedType.Struct)] object RouteWorkbook);

	void _VtblGap2_22();

	[DispId(289)]
	string FullName
	{
		[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
		[LCIDConversion(0)]
		[DispId(289)]
		[return: MarshalAs(UnmanagedType.BStr)]
		get;
	}
}
