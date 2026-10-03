# BIN TOOL MEP - BẢNG ĐIỀU PHỐI LUỒNG CÔNG VIỆC TẬP TRUNG (MASTER WORKFLOW & ROADMAP)
*Tài liệu quản lý trạng thái, kiến trúc và tiến độ phát triển các tính năng của BIN TOOL MEP.*
*Cập nhật lần cuối: 2026-10-03 (Build 0853)*

---

## 1. TRỤC TÍNH NĂNG NGHIỆP VỤ MEP (MEP FEATURE SUITE)

### [ĐÃ HOÀN THÀNH] 1.1 Avoid Clash (Né dầm cho Duct & Pipe)
- **Mã lệnh:** `AvoidClashCmd` · Giao diện: `AvoidClashWindow.cs` · Thuật toán: `AvoidClashGeometry.cs`.
- **Phạm vi hỗ trợ:** Cả **Pipe**, **Round Duct** (ống tròn) và **Rectangular Duct** (ống chữ nhật).
- **3 Chế độ né va chạm:**
  1. `U45`: 4 co 45° chuẩn thủy lực / khí động học êm ái.
  2. `U90`: 4 co 90° vuông góc ôm sát dầm / tiết kiệm trần.
  3. `Z45`: 2 co 45° đổi cao độ luồn qua dầm và tiếp tục chạy thẳng ở vị trí mới (song song trục cũ).
- **Giao diện:** Pure C# WPF Vector Canvas động, hiển thị sơ đồ nguyên lý theo từng mode, phím tắt SPACE/ENTER/ESC.
- **Kế thừa bảo ôn:** Tự động sao chép bảo ôn sang các đoạn mới và tính bảo ôn vào khoảng hở.

### [ĐÃ HOÀN THÀNH] 1.2 Pipe Insulation (Bọc bảo ôn ống)
- **Mã lệnh:** `PipeInsulationCmd` · Giao diện: `PipeInsulationWindow.cs` · Quy tắc: `PipeInsulationRules.cs`.
- **Tính năng:** 5 Presets (Chiller, Nước ngưng, Nước nóng, Đa hệ, Custom), tự động nhận diện theo System Type, bọc kín cả fitting.

### [ĐANG TRIỂN KHAI] 1.3 Check & Né Dầm cho Ống Mềm (Flex Duct & Flex Pipe with Insulation Envelope)
- **Mã lệnh mục tiêu:** Nâng cấp `FlexDuctAvoidMepCmd.cs`, hiện đại hóa `AvoidMepWindow.cs`.
- **Vấn đề cốt tử:** Khắc phục triệt để lỗi bỏ sót lớp bảo ôn (`Insulation Thickness`) khiến vỏ ống mềm bị ăn vào đáy dầm thép chữ I hoặc dầm bê tông link.
- **Thuật toán:**
  1. Tính bán kính ngoài thực tế $R_{total} = R_{nominal} + T_{insulation} + \text{Clearance}$.
  2. Trích xuất Solid 3D thực của dầm (hỗ trợ cả dầm trong file Revit Link).
  3. Rời rạc hóa spline ống mềm, đo khoảng cách hình học ngắn nhất từ các điểm mẫu tới Solid dầm.
  4. Hai chế độ: **Kiểm tra va chạm (Check/Highlight)** và **Tự động uốn né (Auto-Drop Curve Points)**.

---

## 2. TRỤC DỮ LIỆU & TELEMETRY CLOUD (AI-READY OPERATIONS)

### [ĐANG CHẠY LIVE] 2.1 Hệ thống Telemetry V2 Cloudflare + D1
- **Endpoint:** `https://mcp-revit-api.thuongdang531.workers.dev`
- **Dashboard:** `https://mcp-revit-api.thuongdang531.workers.dev/dashboard`
- **Cơ sở dữ liệu:** Cloudflare D1 `mcp-revit-db` (bảng `events_l1`, `events_l2`, `project_events`).
- **Hiệu năng:** Non-blocking tuyệt đối, bounded outbox 200 items / 2 MiB, nén Gzip.

### [ĐÃ THIẾT KẾ XONG] 2.2 Kiến trúc Telemetry V2+ (Codex 6.1 Sol Blueprint)
- **Báo cáo:** `scratch/codex_telemetry_architecture_report.md` (385 dòng).
- **5 Cải tiến chuẩn bị đưa vào code:**
  1. Chuẩn hóa `tool_id` đồng nhất giữa Proxy và Diagnostics.
  2. Khử I/O đồng bộ trong `FlexPipeDiagnostics`, đưa toàn bộ việc ghi log sang worker nền.
  3. Đọc mã phản hồi (Receipt) từ Cloudflare Worker để kiểm soát retry/duplicate.
  4. Bổ sung Deep Signals (Relative Spatial Vectors $\Delta X, \Delta Y, \Delta Z$, flow directions) làm dataset chuẩn huấn luyện AI Agent tự động vẽ MEP.
  5. Cơ chế Self-Healing: Đẩy đầy đủ `callsite`, `stack_hash`, `exception_message` để AI đọc từ xa và fix bug tự động.

---

## 3. TRỤC ĐÓNG GÓI, CÀI ĐẶT & VẬN HÀNH (DISTRIBUTION & OPS)

### [SẴN SÀNG] 3.1 Hot-Reload Trực tiếp (`HotUpdate.cmd`)
- Cập nhật DLL mới vào phiên làm việc Revit đang mở bằng Shadow DLL (`BIN.HotReload.<timestamp>.dll`), không bị Revit khóa file, không cần tắt Revit.

### [SẴN SÀNG] 3.2 Bộ Cài Portable 1-Click No-Admin ZIP (`Install.cmd`)
- Tự động đóng gói qua `Package-BIN.ps1`, không đòi hỏi quyền Administrator của Windows.
- Tự động đẩy lên Google Drive và tạo link trực tiếp qua Gofile.
