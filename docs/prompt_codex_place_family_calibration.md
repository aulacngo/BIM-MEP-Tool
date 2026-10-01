# Yêu cầu Kỹ thuật cho Codex: Nâng cấp Tool Place Family với Chế độ Cân chỉnh Mốc Tọa độ (Grid / Reference Point Calibration)

## 1. Bối cảnh & Vấn đề
- Tool **Place Family** (`PlaceFamily.cs`, `PlaceFamilyWindow.cs`, `PlaceFamilyUtils.cs`) dùng để quét các Block trên bản vẽ CAD Link (như đầu phun Sprinkler, miệng gió, van, thiết bị MEP) và tự động đặt Family Revit tương ứng vào mô hình.
- **Hiện tượng lỗi:** Khi đặt Family (ví dụ Sprinkler), Revit sinh ra ElementId hợp lệ nhưng đối tượng bị văng ra xa hàng trăm mét hoặc hàng chục kilômét so với công trình, không hiển thị trên mặt bằng.
- **Nguyên nhân:** 
  1. File CAD kiến trúc/kết cấu thường dùng hệ tọa độ tổng mặt bằng/trắc địa hoặc vẽ cách xa gốc $(0,0)$ của CAD. Khi link vào Revit ở chế độ Center-to-Center hoặc được dời thủ công về lưới trục công trình, phép đọc tọa độ Block từ CAD bị lệch mốc gốc so với mô hình Revit.
  2. Đầu phun Sprinkler đặt ở cao độ trần ($\ge 2.8\text{m}$ so với sàn), nằm ngoài khoảng View Range cắt của Floor Plan thông thường nên bị ẩn nếu người dùng không mở Ceiling Plan (RCP) hoặc 3D.

## 2. Mục tiêu Triển khai
Thêm tính năng **Cân chỉnh mốc tọa độ theo điểm tham chiếu (Grid / Point Calibration)** cho tool `PlaceFamily`:
1. Cho phép người dùng tùy chọn cân chỉnh mốc tọa độ bằng cách pick 1 điểm mốc trên CAD (ví dụ giao điểm trục X/Y, tim cột) và pick điểm mốc tương ứng trên Revit.
2. Tự động tính vector dịch chuyển $\vec{\Delta} = (\Delta X, \Delta Y)$ và áp dụng cho toàn bộ các điểm đặt của Block, đảm bảo các Family được đặt chính xác $100\%$ vào đúng vị trí mặt bằng công trình.
3. Bổ sung thông báo kiểm tra View Range thông minh nếu đối tượng đặt ở cao độ trần trên View Mặt bằng sàn (Floor Plan).
4. Đồng bộ mã nguồn cho cả 2 target: `.NET Framework 4.8` (`src/net48/`) và `.NET 8.0 Windows` (`src/net8.0-windows/`).

---

## 3. Chi tiết Kỹ thuật Cần Sửa

### A. Giao diện `PlaceFamilyWindow.cs` (Cả net8.0-windows và net48)
- Bổ sung 1 CheckBox `cbCalibrateBasepoint`:
  - Nội dung nhãn: `Cân chỉnh mốc theo giao điểm trục / điểm mốc (Grid Calibration)`
  - Thuộc tính mặc định: `IsChecked = true`
  - Expose property công khai: `public bool IsCalibrateBasepoint { get; set; }`
  - Đặt CheckBox này vào layout Grid của giao diện (ví dụ ngay dưới ô nhập Elevation hoặc trên các nút OK/Cancel).
  - Khi người dùng bấm `btOk_Click`, lưu trạng thái:
    `IsCalibrateBasepoint = cbCalibrateBasepoint.IsChecked == true;`

### B. Luồng thực thi `PlaceFamily.cs` (Cả net8.0-windows và net48)
- Trong phương thức `Execute`, sau khi `window.ShowDialog() == true`:
  - Lấy cờ `bool calibrate = window.IsCalibrateBasepoint;`
  - Nếu `calibrate == true`:
    - Dùng `uidoc.Selection.PickPoint`:
      ```csharp
      XYZ cadRefPoint = uidoc.Selection.PickPoint(
          ObjectSnapTypes.Intersections | ObjectSnapTypes.Endpoints | ObjectSnapTypes.Centers | ObjectSnapTypes.Nearest,
          "BƯỚC 1/2: Click chọn 1 điểm mốc trên bản vẽ CAD (ví dụ: giao điểm 2 trục X/Y hoặc tim cột)"
      );
      XYZ revitRefPoint = uidoc.Selection.PickPoint(
          ObjectSnapTypes.Intersections | ObjectSnapTypes.Endpoints | ObjectSnapTypes.Centers | ObjectSnapTypes.Nearest,
          "BƯỚC 2/2: Click chọn điểm mốc tương ứng trên Revit (giao điểm trục X/Y hoặc tim cột tương ứng)"
      );
      ```
    - Tính vector lệch 2D:
      ```csharp
      double deltaX = revitRefPoint.X - cadRefPoint.X;
      double deltaY = revitRefPoint.Y - cadRefPoint.Y;
      ```
    - Nếu người dùng bấm `Esc` trong lúc PickPoint: bắt `Autodesk.Revit.Exceptions.OperationCanceledException` và hủy lệnh nhẹ nhàng (`return Result.Cancelled;`).
  - Khi duyệt danh sách `pointsToPlace` trong Transaction đặt Family:
    - Hiệu chỉnh tọa độ XY trước khi đặt:
      ```csharp
      double finalX = cadLocation.X + (calibrate ? deltaX : 0.0);
      double finalY = cadLocation.Y + (calibrate ? deltaY : 0.0);
      XYZ revitLocation = new XYZ(finalX, finalY, level.Elevation + elevation);
      ```
  - **Cảnh báo View Range thông minh:**
    - Sau khi `t.Commit()`, kiểm tra nếu View hiện tại là `ViewType.FloorPlan` và `elevation > 2000.0 / 304.8`:
      Bổ sung vào TaskDialog kết thúc:
      `"\n\nLưu ý: Đối tượng được đặt ở cao độ trần ({elevation * 304.8:0} mm), có thể không hiển thị trên Mặt bằng sàn (Floor Plan) do View Range. Vui lòng mở Reflected Ceiling Plan (Mặt bằng trần) hoặc 3D View để quan sát."`

### C. Lưu ý Tương thích .NET 4.8 vs .NET 8.0
- Trong `net48/BIN/PlaceFamily.cs`, dùng `.IntegerValue` hoặc phương thức mở rộng `GetIdInt()` cho `ElementId`.
- Trong `net8.0-windows/BIN/PlaceFamily.cs`, dùng `.Value` hoặc `GetIdInt()`.
- Giữ nguyên cơ chế hot-reload và không làm mất các chức năng lọc điểm trùng `DeduplicatePointsByXY` hay kiểm tra điểm tồn tại `GetExistingSymbolPoints`.

---

## 4. Tiêu chí Nghiệm thu (Acceptance Criteria)
1. Cả 2 target `net48` và `net8.0-windows` biên dịch thành công 0 Error, 0 Warning khi chạy `powershell -ExecutionPolicy Bypass -File .\Dev-BIN.ps1`.
2. Hộp thoại `PlaceFamilyWindow` hiển thị rõ ràng CheckBox cân chỉnh mốc.
3. Khi bật CheckBox, người dùng pick 2 điểm mốc (CAD $\rightarrow$ Revit) thành công, các family được đặt chính xác vào đúng tọa độ tương đối của lưới trục Revit, không còn bị văng ra xa.
4. Khi tắt CheckBox, tool hoạt động theo tọa độ link CAD tự động như cũ.
5. TaskDialog kết thúc có hướng dẫn kiểm tra View Range/Ceiling Plan nếu đặt ở cao độ trần.
