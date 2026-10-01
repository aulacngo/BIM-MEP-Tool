# BIN TOOL Development Workflow

## Cài loader một lần

Chạy lệnh sau. Có thể chạy khi Revit đang mở; loader mới có hiệu lực ở lần mở Revit tiếp theo:

```powershell
powershell -ExecutionPolicy Bypass -File ".\Dev-BIN.ps1" -InstallLoader
```

Mở lại Revit. Tất cả Ribbon button của BIN TOOL từ thời điểm này đi qua development proxy.

## Build khi Revit đang mở

Sau mỗi lần sửa source, chạy:

```powershell
powershell -ExecutionPolicy Bypass -File ".\Dev-BIN.ps1"
```

Không cần đóng Revit. Lần bấm Ribbon button tiếp theo sẽ load `BIN.dll` vừa build.

Chỉ build một runtime:

```powershell
powershell -ExecutionPolicy Bypass -File ".\Dev-BIN.ps1" -Runtime net48
powershell -ExecutionPolicy Bypass -File ".\Dev-BIN.ps1" -Runtime net8
```

`net48` dùng cho Revit 2022-2024. `net8` dùng cho Revit 2025-2026.

## Đọc diagnostic

```powershell
powershell -ExecutionPolicy Bypass -File ".\Dev-BIN.ps1" -Diagnostics
powershell -ExecutionPolicy Bypass -File ".\Dev-BIN.ps1" -Journal
```

MCP endpoints tương ứng:

```text
GET http://localhost:8077/commands/latest?n=100
GET http://localhost:8077/commands/last-error
GET http://localhost:8077/journal/latest?n=400
GET http://localhost:8077/selection
GET http://localhost:8077/ping
GET http://localhost:8077/views/open
GET http://localhost:8077/cad/links
GET http://localhost:8077/cad/inspect?id=<ImportInstanceId>
GET http://localhost:8077/cad/inspect?id=<ImportInstanceId>&block=<CADBlockName>&n=50
```

Command diagnostic được lưu tại:

```text
%LOCALAPPDATA%\BIN TOOL\Diagnostics\commands.jsonl
```

Mỗi event có command, stage, result, document, view, selection IDs, message, DLL được load và exception/stack trace.

## Đọc CAD Link qua Hot API

`/cad/links` chỉ đọc metadata của từng CAD link/import: ID, path, linked/imported state, transform và scale.

`/cad/inspect?id=...` trả block names và layer names của một CAD link. Thêm `block=...` để trả insertion points theo tọa độ project (mm), tối đa `n` điểm (1–200), cùng diagnostics về DWG/Xref/units. Các endpoint này chỉ đọc; chưa tạo hay sửa element Revit.

`/views/open` trả mọi view UI đang mở trong document, gồm ID, tên, loại view, active state và zoom corners theo mm.

## Giới hạn

### Sprinkler Flex Multi — hai chế độ

Cập nhật 2026-09-23: chế độ thứ hai là **Nhánh Tê + Elbow**, thay cho chỉ fitting có sẵn. Quét toàn bộ một tuyến ống nhánh nằm ngang liên thông và sprinkler. Dùng lại đầu chờ fitting hướng xuống; một đầu ống cuối hở được ghép tạo Elbow; sprinkler còn lại được chiếu lên đoạn ống để tạo Tê. Chưa đọc block CAD. Khi không chọn ống, vẫn có thể chọn đủ fitting có sẵn và sprinkler.

Nhánh nhiều đầu hở, thiếu đoạn giữa, sprinkler đã nối, Tê cách đầu đoạn dưới 100 mm hoặc hai Tê cách nhau dưới 100 mm sẽ bị từ chối. Ghép tự động cần kiểm tra trong bảng xác nhận. Toàn cụm rollback khi lỗi; thành công chọn phần tử mới. Chưa kiểm chứng live nhánh hỗn hợp; cần test 2 Tê + 1 Elbow, fitting có sẵn và nhánh có reducer. Code chia sẻ: `src/net8.0-windows/BIN/FlexBranchMulti.cs`.

- Elbow: giữ luồng Multi trước đây.
- Fitting có sẵn: chọn Tê/Cút và sprinkler, hoặc chọn ống nhánh kèm sprinkler để lấy các fitting gắn trực tiếp trên các ống đã chọn. Mỗi fitting phải có đúng một connector piping hở hướng xuống và nối với Pipe. Fitting không có đầu hở được bỏ qua. Số đầu chờ phải bằng số sprinkler có đầu hở.
- Ghép một-một theo tổng khoảng cách 3D nhỏ nhất, hiển thị ID và khoảng cách để xác nhận trước khi tạo. Chưa nhận diện ký hiệu CAD hay bảo đảm đường flex không giao nhau. Giữ fitting trên nhánh; reducer phía sprinkler vẫn theo tool đơn. Lỗi một cặp sẽ rollback cả cụm.
- Build net48 dùng chung source ConnectSprinklerFlexPipeCmd.cs từ net8.0-windows. Cần test live 2 Tê + 1 Elbow với 3 sprinkler, kiểm connector kín và rollback khi một cặp lỗi.

- Ribbon và `Panel.OnStartup()` chỉ cập nhật sau khi đóng/mở Revit và chạy lại `-InstallLoader`.
- Window modeless hoặc `ExternalEvent` đang mở tiếp tục dùng assembly đã tạo ra chúng; đóng window rồi bấm command lại sau khi build.
- Mỗi lần chạy command sẽ load một assembly mới. Sau nhiều vòng debug hoặc khi thay dependency, nên khởi động lại Revit để giải phóng assembly cũ.
- WPF resource dùng pack URI có thể bị ảnh hưởng bởi nhiều assembly cùng tên. UI dựng trực tiếp bằng code hoặc resource URI không phụ thuộc assembly mặc định sẽ ổn định hơn trong chế độ hot reload.
