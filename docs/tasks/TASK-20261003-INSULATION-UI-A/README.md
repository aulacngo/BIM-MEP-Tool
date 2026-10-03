# Pipe & Duct Insulation — phương án A

Đã bỏ ComboBox chọn loại insulation phía trên và thuộc tính `SelectedInsulationType`. Khung “Thiết lập insulation” chỉ còn hai checkbox thay thế insulation và làm mượt Tê. Cột 5 của bảng quy tắc giữ lựa chọn loại riêng cho từng dòng, với danh sách type đúng nhóm Pipe/Duct.

Dòng mới dùng type đầu tiên theo tên của nhóm hiện tại. Preset thiếu loại hoặc còn nhãn cũ được chuyển sang tên type thực tế; Smart Match chỉ chọn khi có một ứng viên. Khi lưu/Apply, tên type không còn trong document được chuẩn hóa về type đầu tiên. Nếu nhóm hiện tại không có type, Apply hiển thị thông báo và giữ cửa sổ mở.

Command lấy type theo `matchingRule.InsulationTypeName`, fallback qua `GetDefaultInsulationTypeId(doc, isDuct)` cho cả Pipe, Duct và fitting. Kiểm tra thiếu type trước khi thực hiện và chặn ID null/invalid trước khi xóa insulation cũ; giữ cơ chế rollback theo host.

## Kiểm chứng

- `dotnet build src/net48/BIN.csproj -c Release --no-restore -v minimal`: 0 warning, 0 error.
- `dotnet build src/net8.0-windows/BIN.csproj -c Release --no-restore -v minimal`: 0 warning, 0 error.
- SHA-256 byte parity: PASS cho `PipeInsulationWindow.cs`, `PipeInsulationCmd.cs`, `PipeInsulationRules.cs`; xem [source-hashes.json](source-hashes.json).
- `git diff --check`: PASS; không còn tham chiếu `insulationTypeComboBox` hay `SelectedInsulationType` trong mã nguồn insulation.
- 18 kiểm tra standalone WPF: PASS; bao gồm cột loại, fallback rule trống/preset cũ/type bị mất, lựa chọn riêng từng dòng, thêm/xóa dòng, nạp lại preset, lưu/nạp JSON, chuyển Pipe/Duct, bố cục checkbox và nhóm không có type. Harness nằm ở `scratch/insulation-ui-a`; save/load dùng file tạm và không ghi preset người dùng.

Log cục bộ: `build-net48.log`, `build-net8.log`, `ui-smoke.log` trong thư mục này.

## Preview

Ảnh render từ WPF với dữ liệu mẫu, không phải ảnh hot-load Revit:

- [Pipe](pipe-preview.png)
- [Duct](duct-preview.png)

Chưa kiểm tra hot-load DLL, thao tác trên model Revit hay Schedule/BOQ trong phiên này.
