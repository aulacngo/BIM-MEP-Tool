# Yêu cầu Kỹ thuật cho Codex: Bổ sung Tùy chọn Đơn vị CAD (CAD Unit Selector) & Fix Lỗi Tỷ lệ Đơn vị cho Tool Place Family

## 1. Bối cảnh & Mục tiêu
- **Vấn đề đã xác định:** Khi file CAD có đơn vị `INSUNITS = 0` (`Undefined`), hàm chuyển đổi đơn vị trong `PlaceFamilyUtils.cs` trả về hệ số `1.0` (hiểu 1 đơn vị CAD = 1 foot trong Revit) và logic `transformAlreadyConvertsUnits` nhận nhầm tỷ lệ `1.0 == 1.0`. Hậu quả là toàn bộ block bị phóng đại $304.8$ lần, văng xa hàng trăm cây số.
- **Yêu cầu mới từ người dùng:** Bổ sung trực tiếp ô chọn đơn vị **CAD Unit** trên giao diện `PlaceFamilyWindow` để người dùng chủ động chọn đơn vị của bản vẽ CAD (Hệ mét: mm, cm, m; Hệ inch/feet cho dự án nước ngoài/Mỹ), đồng thời hỗ trợ chế độ **Auto Detect (Tự động nhận diện)**.

---

## 2. Chi tiết Kỹ thuật Cần Sửa

### A. Giao diện `PlaceFamilyWindow.cs` (Cả `src/net8.0-windows/` và `src/net48/`)
1. Thêm ComboBox `cbbCadUnit` và thuộc tính:
   ```csharp
   internal ComboBox cbbCadUnit;
   public string SelectedCadUnit { get; set; } = "Auto Detect (Tự động)";
   ```
2. Khởi tạo danh sách các đơn vị trong `InitializeComponent`:
   - Danh sách Items:
     - `Auto Detect (Tự động)`
     - `Millimeters (mm)`
     - `Meters (m)`
     - `Centimeters (cm)`
     - `Inches (in)`
     - `Feet (ft)`
   - Mặc định: `cbbCadUnit.SelectedIndex = 0;` (`Auto Detect (Tự động)`).
3. Đưa `cbbCadUnit` vào bố cục lưới (Layout Grid):
   - Thêm hàng hiển thị: `AddRow("CAD Unit", cbbCadUnit, 1);`
   - Dời các hàng phía dưới (`CAD Block`, `Family`, `Type`, `Level`, `Elevation`, `Grid Calibration`, Buttons) tăng thêm 1 index hàng tương ứng.
4. Khi bấm `btOk_Click`:
   - Gán: `SelectedCadUnit = cbbCadUnit.SelectedItem != null ? cbbCadUnit.SelectedItem.ToString() : "Auto Detect (Tự động)";`

---

### B. Hàm Xử lý Đơn vị trong `PlaceFamilyUtils.cs` (Cả `src/net8.0-windows/` và `src/net48/`)
1. Thêm phương thức chuyển đổi từ chuỗi đơn vị người dùng chọn sang hệ số Feet:
   ```csharp
   public static double ParseCadUnitToFeetFactor(string unitName)
   {
       if (string.IsNullOrWhiteSpace(unitName)) return 0.0;
       if (unitName.IndexOf("Millimeter", StringComparison.OrdinalIgnoreCase) >= 0 || unitName.IndexOf("(mm)", StringComparison.OrdinalIgnoreCase) >= 0)
           return 1.0 / 304.8;
       if (unitName.IndexOf("Meter", StringComparison.OrdinalIgnoreCase) >= 0 || unitName.IndexOf("(m)", StringComparison.OrdinalIgnoreCase) >= 0)
           return 1.0 / 0.3048;
       if (unitName.IndexOf("Centimeter", StringComparison.OrdinalIgnoreCase) >= 0 || unitName.IndexOf("(cm)", StringComparison.OrdinalIgnoreCase) >= 0)
           return 1.0 / 30.48;
       if (unitName.IndexOf("Inch", StringComparison.OrdinalIgnoreCase) >= 0 || unitName.IndexOf("(in)", StringComparison.OrdinalIgnoreCase) >= 0)
           return 1.0 / 12.0;
       if (unitName.IndexOf("Feet", StringComparison.OrdinalIgnoreCase) >= 0 || unitName.IndexOf("(ft)", StringComparison.OrdinalIgnoreCase) >= 0)
           return 1.0;
       return 0.0; // 0.0 nghĩa là Auto Detect
   }
   ```
2. Sửa hàm `GetDrawingUnitToFeetFactor(AcDb.UnitsValue units)`:
   - Khi `units == AcDb.UnitsValue.Undefined`: **KHÔNG ĐƯỢC trả về 1.0**. Hãy trả về `1.0 / 304.8` (mặc định cho các bản vẽ kỹ thuật xây dựng hệ Mét).
   - Với các đơn vị khác, giữ nguyên gọi `AcDb.UnitsConverter.GetConversionFactor(units, AcDb.UnitsValue.Feet)`.
3. Sửa hàm `GetBlockPointsFromDwg`:
   - Nhận thêm tham số `double manualUnitScale = 0.0`:
     `private static List<XYZ> GetBlockPointsFromDwg(ImportInstance importInstance, CADLinkType cadLinkType, string blockName, double manualUnitScale = 0.0)`
   - Xác định `unitToFeet`:
     - Nếu `manualUnitScale > 0.0`: `unitToFeet = manualUnitScale;`
     - Ngược lại (Auto Detect):
       - Nếu `drawingUnits != AcDb.UnitsValue.Undefined`: `unitToFeet = GetDrawingUnitToFeetFactor(drawingUnits);`
       - Nếu `drawingUnits == AcDb.UnitsValue.Undefined`:
         - Tự động nhận diện: Nếu tọa độ block hoặc extents trong database $> 50.0$ (thường là hàng nghìn mm) $\rightarrow$ `unitToFeet = 1.0 / 304.8`.
         - Mặc định an toàn là `1.0 / 304.8`.
   - **LOẠI BỎ TRIỆT ĐỂ** dòng kiểm tra sai:
     ```csharp
     // XÓA BỎ 2 DÒNG NÀY:
     // bool transformAlreadyConvertsUnits = NearlyEqualScale(importScale, unitToFeet);
     // double pointScale = transformAlreadyConvertsUnits ? 1.0 : unitToFeet;
     ```
     **THAY BẰNG:**
     ```csharp
     double pointScale = unitToFeet;
     ```
     *(Lý do: Trong Revit API, `ImportInstance.GetTotalTransform().Scale` luôn luôn bằng 1.0, không bao giờ tự scale đơn vị của DWG. Toàn bộ điểm đọc trực tiếp từ DWG bắt buộc phải nhân với `pointScale = unitToFeet` trước khi biến đổi qua `importTransform.OfPoint`).*
4. Cập nhật phương thức `GetListBlockCadByName`:
   - Thêm tham số `string selectedCadUnit = "Auto Detect (Tự động)"`:
     `public static List<XYZ> GetListBlockCadByName(ImportInstance importInstance, CADLinkType cadLinkType, string blockName, string selectedCadUnit = "Auto Detect (Tự động)")`
   - Tính `double manualUnitScale = ParseCadUnitToFeetFactor(selectedCadUnit);` và truyền vào `GetBlockPointsFromDwg`.

---

### C. Luồng thực thi `PlaceFamily.cs` (Cả `src/net8.0-windows/` và `src/net48/`)
1. Trong phương thức `Execute`, sau khi đóng cửa sổ `window`:
   - Lấy đơn vị đã chọn: `string selectedCadUnit = window.SelectedCadUnit;`
   - Gọi quét block với đơn vị tương ứng:
     `List<XYZ> listBlockCad = PlaceFamilyUtils.GetListBlockCadByName(filecad, cadLinkType, cadBlock, selectedCadUnit);`

---

## 3. Tiêu chí Nghiệm thu (Acceptance Criteria)
1. Cả 2 target `net48` và `net8.0-windows` biên dịch thành công 0 Error, 0 Warning khi chạy:
   `powershell -ExecutionPolicy Bypass -File .\Dev-BIN.ps1`
2. Cửa sổ `PlaceFamilyWindow` có mục `CAD Unit` với các lựa chọn: `Auto Detect (Tự động)`, `Millimeters (mm)`, `Meters (m)`, `Centimeters (cm)`, `Inches (in)`, `Feet (ft)`.
3. Khi chọn bất kỳ đơn vị nào (kể cả Inches cho dự án nước ngoài), các block CAD được đổi tỷ lệ chính xác 100% sang Feet của Revit và đặt đúng vị trí.
4. Khi để `Auto Detect (Tự động)`, file CAD dù có `INSUNITS = 0` (`Undefined`) vẫn tự động nhận diện đúng Millimeters (`1.0 / 304.8`), không bị văng xa $100\text{ km}$ như trước.
