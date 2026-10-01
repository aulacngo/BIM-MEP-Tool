# BIN TOOL - Development & Workflow Guide

## Architecture & Multi-Target Frameworks
- src/net48/BIN.csproj: Revit 2019 - 2024 (.NET Framework 4.8)
- src/net8.0-windows/BIN.csproj: Revit 2025+ (.NET 8.0 Windows)

## Build, Audit & Hot Reload Procedures
- Single Standard Command: `powershell -ExecutionPolicy Bypass -File .\Dev-BIN.ps1`
  - Automatically compiles both .NET 4.8 and .NET 8 targets.
  - Automatically runs pre-flight quality audit `Test-BinAddinAudit.ps1`.
  - Automatically updates Hot-Reload path for instant in-session execution.
  - Automatically stages and updates the DevLoader bundle and Revit manifests.
- Pre-Flight Quality Audit: `powershell -ExecutionPolicy Bypass -File .\Test-BinAddinAudit.ps1`
  - Verifies 100% button-to-proxy matching, transaction attributes, and command class presence.

## Ribbon Structure (75 Commands across 2 Tabs & 10 Panels)

### TAB 1: BIM - MEP
1. **BIM - MICRO** (8 Tools):
   - Move Connect & Disconnect (SplitButton)
   - Trim 3D (PushButton)
   - Elbow 90° (SplitButton: Up 90°, Down 90°, Left 90°, Right 90°)
   - Elbow 45° (SplitButton: Up 45°, Down 45°, Left 45°, Right 45°)
   - Rotate Elements (PushButton)
   - Rotate Multi (PushButton)
   - Quick Alignment (SplitButton: 3D Align, Branch Lite, Align Pipe Elevation)
   - System Cleanup (Pulldown: Delete System, Delete Orphan Insulation)
2. **BIM - MEP** (16 Tools): Avoid Clash, Bloom, Split Duct, Place Family, Rotate Family, Change Ref Level, Toggle Insulation, Turn MEP, MEP Offset, Cut MEP By Level, Pipe Insulation, Draw Multi Pipe, Manage Element, Select By Parameter, Compare Element, Clash Detective.
3. **BIM - SPRINKLER** (11 Tools): Optimize Pipes, Replace Pipe With Nipple, Shorten Sprinkler, Reducing Elbow, Sprinkler Flipper, Remove Coupling, Sprinkler To Pipe, Sprinkler Flex Pipe, Sprinkler Flex Multi, Tee/Reducer Groove, Check Sprinkler.
4. **BIM - DRAINAGE** (6 Tools): Connect Vent Riser, Create Elbows 45 deg, Join 2 Elbows, Turn Drainage, Draw Drainage, Draw Clean Out.
5. **BIM - SUPPORT** (4 Tools): Multi Pipe Support, Place Support, Align Support, Move Support.
6. **BIM - CHECK** (7 Tools): Check Air Terminal, Check Pipe Fitting, Check Connected Pipe, Check Undefined Pipe, Flex Duct Avoid MEP, Get ID, Pipe Clash Clearance.

### TAB 2: BIM - DOCS
7. **BIM - BOQ & EXCEL** (2 Tools): BOQ, Schedule To Excel.
8. **BIM - SHEET & VIEW** (4 Tools): Sheet From Excel, Copy Sheet, Copy View, Copy Filter.
9. **BIM - 2D ANNOTATION** (4 Tools): Grid & Level, Section Head/Tail, Delete 2D Element, Rename 2D Element.
10. **BIM - TRANSFER & LINK** (3 Tools): Export Family, Transfer Document, Copy From Link.

## Rules for Adding New Commands
- When adding a new command class:
  1. Add the ExternalCommand in BIN/<CommandName>.cs.
  2. Add the corresponding DevProxy_<CommandName> in DevCommandProxy.cs.
  3. Register the button in Panel.cs using CreatePushData.
  4. Run .\Dev-BIN.ps1 to build and stage everything automatically.

## Automated Release Packaging (No-Admin Portable Zip)
- Command: `powershell -ExecutionPolicy Bypass -File .\Package-BIN.ps1`
  - Compiles both .NET 4.8 and .NET 8 Release DLLs.
  - Generates portable directory `release/BIN-Tool-Portable-<YYYYMMDD>-Current/` with `Install.cmd`.
  - Creates 1-click installer zip `release/BIN-Tool-CurrentUser-NoAdmin-<YYYYMMDD>.zip`.
  - Skill: `bin-addin-packaging` triggers automatically when user asks to "đóng gói", "zip tool", "package".

## Quy định Phân Quyền Tuyệt Đối Giữa Antigravity và Codex
1. **Nghiêm cấm Antigravity tự ý sửa code sản phẩm (`src/`):**
   - Antigravity đóng vai trò Lead Coordinator: Phân tích yêu cầu, tra cứu tài liệu/bản vẽ, lên kế hoạch kiến trúc, đóng gói prompt và kích hoạt Codex.
   - TUYỆT ĐỐI KHÔNG dùng tool `replace_file_content` hoặc `write_to_file` trên bất kỳ file nào trong thư mục `src/` để sửa code, fix bug, hay tinh chỉnh giao diện.
2. **Ủy quyền toàn bộ việc viết và sửa mã nguồn cho Codex:**
   - Khi cần tạo tính năng mới, sửa lỗi, refactor, hoặc cải tiến: Antigravity BẮT BUỘC đóng gói prompt chi tiết và gọi Codex CLI (`codex.exe exec ...`) với model được User chỉ định (ví dụ `gpt-5.6-terra`, `gpt-6-astra`).
   - Mọi thao tác chỉnh sửa hoặc tạo mới file mã nguồn trong `src/` PHẢI do Codex trực tiếp thực hiện 100%.
   - Sau khi Codex hoàn thành, Antigravity chỉ chạy lệnh biên dịch (`dotnet build`) để kiểm tra trạng thái và báo cáo trung thực kết quả cho User.

