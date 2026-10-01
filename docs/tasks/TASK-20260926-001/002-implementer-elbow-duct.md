# TASK-20260926-001 / DSP-002 — Kết quả implementation

Ngày: 2026-09-26, Asia/Saigon. Role: implementer, self-check; chưa có review độc lập.

**Đã triển khai sửa nhánh Duct của cả 8 nút Elbow 90°/45° và build thành công cả net48 lẫn net8.0-windows. Trạng thái: PASS_OFFLINE; runtime Revit và activation/deploy chưa thực hiện.**

Quyền thực hiện theo `D:\Computer\tasks\TASK-20260926-001\DECISIONS.md#D002` và dispatch của User. Đã đọc `D:\Computer\governance\ROLES.md`, `RULES.md`, `D:\Tool Revit\GEMINI.md`, `DEVELOPMENT.md` và báo cáo DSP-001. Không tìm thấy `AGENTS.md` trong project. Requested model: `gpt-6-astra`, reasoning `high`; actual model/effort: công cụ không cung cấp metadata xác minh độc lập.

Workspace không có Git repository tại thời điểm kiểm tra (`git status`: not a git repository). Baseline dùng SHA-256 toàn bộ các file source/resource được liệt kê trong `baseline-source-hashes.csv`, cộng bản sao nguyên gốc hai helper cho mỗi target trong `baseline/`. Báo cáo và bằng chứng đặt trong workspace được phép ghi; không ghi vào hub `D:\Computer`.

## Thay đổi đã thực hiện

Mỗi target có cùng năm module dưới `src/<target>/BIN/`; không đổi cấu hình compile hoặc link csproj. Kiểm tra byte/hash bảo đảm đồng bộ lần triển khai này.

| Module | Thay đổi |
|---|---|
| `ElbowHelper.cs` | Dispatch Duct sớm vào engine mới; bỏ nhánh Duct cũ; gán `message`, ghi exception và hiện TaskDialog khi lỗi. Các thuật toán hướng cũ còn lại chỉ phục vụ Pipe/Conduit/CableTray. |
| `NaviateHelper.cs` | Extension của Duct đọc profile an toàn; `DrawDuct` ủy quyền builder và truyền lỗi ra ngoài. Bỏ đường Duct nuốt lỗi/return im lặng/`ConnectTo` dự phòng. |
| `DuctProfileData.cs` — mới | Shape, đọc/ghi/đọc lại size, extension, đúng system type ID và reference level. |
| `ElbowGeometry.cs` — mới | Hướng 45°/90°, phép vận chuyển frame qua mặt phẳng uốn, góc chỉnh roll. |
| `DuctElbowBuilder.cs` — mới | Chọn đầu, preflight, transaction/subtransaction/group, tạo duct và elbow, chỉnh roll, kiểm profile/topology trước và sau commit. |

**Không thay đổi** cả hai bản `Create2Elbows45.cs`, `Join2Elbows.cs`; tám wrapper command; `Panel.cs`, `DevCommandProxy.cs`; chữ ký/tên command; các file source/resource khác có trong baseline. Không sửa model, routing preferences, family, cấu hình loader hoặc manifest. Không chạy `Dev-BIN.ps1`, package, restart hoặc activate.

### Shape và kích thước

- Đọc `connector.Shape`, đối chiếu `duct.DuctType.Shape`.
- Round dùng parameter `RBS_CURVE_DIAMETER_PARAM`.
- Rectangular/Oval dùng `RBS_CURVE_WIDTH_PARAM` và `RBS_CURVE_HEIGHT_PARAM`.
- Nhánh Duct không gọi getter `MEPCurve.Diameter`, `MEPCurve.Width/Height` hoặc `Connector.Width/Height` để xác định/đọc kích thước.
- Kiểm tra parameter tồn tại, đúng StorageType, hữu hạn/dương; lúc ghi kiểm writable và đọc lại. Giá trị đã đúng được giữ nguyên, tránh coi `Parameter.Set` trả false vì không đổi giá trị là lỗi. Hành vi return của Set được đối chiếu [Autodesk Parameter guide](https://help.autodesk.com/cloudhelp/2023/ITA/Revit-API/files/Revit_API_Developers_Guide/Basic_Interaction_with_Revit_Elements/Parameters/Revit_API_Revit_API_Developers_Guide_Basic_Interaction_with_Revit_Elements_Parameters_Parameter_html.html).
- Chiều dài tim đoạn mới: `max(1 ft, 4 × D)` với Round; `max(1 ft, 4 × max(W,H))` với Rectangular/Oval. Không nhân thêm hệ số 1.414 cho 45°. Đây là khoảng dự trù theo tiết diện, không phải tính bán kính chính xác của mọi family. Không đủ chỗ hoặc family không hỗ trợ thì báo lỗi và rollback; không kéo dài nguồn hoặc chọn family khác tùy ý.

### Hướng và tiết diện

`n` là trục đi ra tại đầu nguồn đã chọn. Với ống không đứng, `up = normalize(n × (Z × n))`, `left = normalize(up × n)`. Đây là phép chiếu global Z lên mặt phẳng vuông góc `n`, viết bằng tích có hướng để tránh mất chính xác khi gần đứng.

- 90°: Up/Down = ±up; Left/Right = ±left.
- 45°: chuẩn hóa `n ± up` hoặc `n ± left`.
- Trong ngưỡng đứng `|n × (Z × n)| <= 1e-8`, dùng `BasisY` làm Up và `BasisX` làm Left, chiếu vuông góc `n`; Down/Right là đối diện. Không đổi đầu riêng cho Down45.
- Quy ước gần đứng có ngưỡng xác định; không hứa hướng liên tục xuyên qua trục Z, nơi global Up không có hình chiếu duy nhất. Cả hai phía ngưỡng và hai cực ±Z đã được kiểm tra số học.
- Rectangular/Oval: xoay cả BasisX/BasisY nguồn qua góc uốn quanh pháp tuyến mặt phẳng uốn, tính roll có dấu, xoay duct mới quanh tim bằng `RotateElement`, regenerate và kiểm lại hai trục. Cho phép đảo dấu 180° của cùng trục tiết diện; không cho tráo Width/Height, kể cả tiết diện vuông.
- Chọn đầu HVAC vật lý gần điểm click nhất. Nếu đầu đó đã nối thì từ chối rõ ràng, không tự chuyển sang đầu hở bên kia. Chỉ nhận duct cứng thẳng trong engine mới.

### System, level và nguyên tử giao dịch

- Ưu tiên source `RBS_DUCT_SYSTEM_TYPE_PARAM`; sau đó dùng type ID của `duct.MEPSystem`/`connector.MEPSystem`. Không tìm theo tên hoặc lấy type đầu tiên. Không xác định được chính xác thì báo lỗi trước tạo.
- Ưu tiên source `ReferenceLevel`, sau đó LevelId hợp lệ; cuối cùng level có `ProjectElevation` gần cao độ đầu nguồn nhất. Không lấy ActiveView level thay nguồn. Hậu kiểm reference level duct mới và giữ reference level nguồn nếu có.
- Group bao trùm transaction và hậu kiểm sau commit. Builder có subtransaction riêng để bảo vệ cả caller của `NaviateHelper.DrawDuct`.
- `NewElbowFitting` là thao tác tạo fitting; không dùng fallback `ConnectTo`. Autodesk công bố lỗi khi hình học hoặc connector không phù hợp; builder truyền lỗi có tên bước ra UI: [NewElbowFitting](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/6f206b7d-f982-bdbd-8342-ed99719e9d81.htm).
- Error/document-corruption trong failure processing trả `ProceedWithRollBack`; failure options dùng clear-after-rollback và forced-modal để không coi trạng thái Pending là thành công. Không tự xóa warning để ép commit. Cơ chế được đối chiếu [Autodesk IFailuresPreprocessor](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/56e273aa-7d84-4a95-f06c-8a12e34e8be0.htm).
- Kiểm kết nối thực tế `IsConnectedTo` của hai đầu fitting với nguồn và duct mới; đầu xa mới còn hở; hướng nguồn/đích đúng; shape, W/H/D, DuctType/system/level đúng; frame tiết diện đúng; đầu xa nguồn và danh sách kết nối ngoài không đổi.
- Kiểm trước commit và sau commit. Chỉ báo Succeeded sau Assimilate thành công; group phục hồi cả transaction đã commit nếu hậu kiểm thất bại. Nguồn được phép trim ở đầu tạo co, đầu xa phải giữ nguyên. Một Undo item khi thành công.
- TaskDialog và `message` báo tên hướng/bước/lỗi. Dùng `SimpleLogger` sẵn có để ghi source ID, shape/size, type/system/level, frame, fitting/new duct ID, transaction status và exception. Logger sẵn có vẫn có giới hạn khi đường log không ghi được; TaskDialog là kênh báo lỗi trực tiếp.

## Bằng chứng self-check

Tất cả command dưới đây chạy từ `D:\Tool Revit`. SDK `dotnet 8.0.416`; Python `C:\Program Files\Python312\python.exe`; Windows PowerShell.

| Kiểm tra | Kết quả quan sát | Bằng chứng |
|---|---|---|
| net48 Release, API Revit 2023 theo csproj | Exit 0; 0 warnings; 0 errors | `build-net48.log` |
| net8.0-windows Release, API Revit 2026 theo csproj | Exit 0; 0 warnings; 0 errors | `build-net8.0-windows.log` |
| Geometry C# thực tế, XYZ test double | Exit 0; 2.082 frame × 8 hướng; 322.628 assertions | `geometry-tests.log`, `scripts/tests/ElbowGeometry/` |
| Add-in integrity audit | Exit 0; 75 button mỗi target; 0 errors/warnings | `addin-audit.log` |
| Source/scope/hash | Exit 0; 4 file cũ đổi, 6 file production mới; 5 cặp đồng nhất; exclusions/mapping nguyên vẹn | `scope-verification.json`, `verify_scope.py` |

Audit script có câu mặc định “Ready for deployment”; câu đó chỉ là text của script, **không phải chứng nhận runtime hoặc quyền deploy của dispatch này**.

Lệnh build, lặp cho `$target` bằng `net48` và `net8.0-windows`:

```powershell
dotnet build "D:\Tool Revit\src\$target\BIN.csproj" -c Release --no-restore --nologo "-p:OutputPath=D:/Tool Revit/docs/tasks/TASK-20260926-001/build/$target/" "-p:IntermediateOutputPath=D:/Tool Revit/docs/tasks/TASK-20260926-001/obj/$target/"
```

Output và intermediate nằm riêng trong task, không ghi đè DLL Release thông thường mà hot loader có thể theo dõi. Dùng NuGet assets sẵn có qua `--no-restore`. Build thành công với warning suppressions hiện có của project; không thêm suppression.

```powershell
dotnet run --project "D:\Tool Revit\scripts\tests\ElbowGeometry\ElbowGeometry.Tests.csproj" -c Release --no-restore
powershell -ExecutionPolicy Bypass -File "D:\Tool Revit\Test-BinAddinAudit.ps1" -SolutionDir "D:\Tool Revit"
python "D:\Tool Revit\docs\tasks\TASK-20260926-001\verify_scope.py"
```

Geometry harness compile trực tiếp production `ElbowGeometry.cs` và enum; dùng test double chỉ cho số học XYZ, không load/thực thi Revit. Bao gồm đáp án độc lập cho các trục chính, dốc 30°, hướng đối nhau, góc/unit length, vận chuyển frame trực chuẩn, roll có dấu, hai phía ngưỡng đứng, roll 90° và góc bất kỳ, 2.000 frame ngẫu nhiên có seed, input không hợp lệ.

Lần test đầu đã phát hiện mất thành phần Z gần đứng trong công thức trừ trực tiếp; đã sửa rồi chạy lại đạt. Log lỗi giữ ở `geometry-tests-attempt1.log`. Lần gọi build đầu có lỗi quote đường dẫn PowerShell do backslash cuối chuỗi, trước compiler; đã đổi property path sang `/` và build đạt. Log giữ ở `build-<target>-attempt1.log`.

SHA-256 source và DLL **cuối cùng** nằm trong `scope-verification.json`. Diffs hai helper so với baseline nằm trong `net48-*.diff`, `net8.0-windows-*.diff`. Các file mới nằm trực tiếp trong source và được default compile item hiện có đưa vào cả hai build.

## Giới hạn và nghiệm thu tiếp theo

**Chưa kiểm chứng runtime**: tạo fitting thật, API parameter Oval theo family thực tế, roll với routing library, lỗi lúc regenerate/commit, rollback model, Undo, dispatch/hot reload, hồi quy thực tế Pipe/Conduit/CableTray. Build với references 2023 và 2026 không chứng nhận mọi phiên bản 2019–2026. Không kết nối phiên Revit hoặc gọi endpoint thao tác model trong dispatch này.

Nghiệm thu cần thực hiện khi có quyền runtime/activation riêng, trên bản sao model có routing xác định:

1. Trên Revit 2023 và 2026: Round/Rectangular/Oval × ±X/±Y/±Z × 8 nút, tối thiểu 144 ca/phiên bản; thử cả hai đầu. Rectangular thêm 600×300, 300×600, 400×400; Oval W/H khác nhau.
2. Ngang xoay XY 30°/45°, dốc lên/xuống, gần đứng hai phía ngưỡng, tiết diện roll 90° và góc bất kỳ. Đầu đã nối phải báo lỗi, không chuyển sang đầu đối diện; Down45 ở đầu hướng lên vẫn dùng đúng đầu đã chọn.
3. Supply/Return/Exhaust với tên custom, source thiếu MEPSystem nhưng có type parameter, view level khác source level. Không xác định được system type phải thất bại rõ.
4. Thiếu family/routing elbow, không có góc 45°, kích thước không hỗ trợ, nguồn ngắn/tiết diện lớn, lỗi size/rotation/fitting/commit: ghi element census trước/sau; thất bại phải không còn phần tử mới và phục hồi nguồn/kết nối ngoài.
5. Thành công: đúng một duct mới và một elbow; graph nối đúng; angle, W/H/D, roll, system/type/level đúng; đầu xa giữ nguyên; một Undo hoàn tác toàn bộ. Kiểm Ribbon/proxy và DLL thực sự được tải.
6. Hồi quy tám nút với Pipe/Conduit/CableTray; các nhánh hình học cũ được giữ nguyên nhưng chưa test runtime trong lượt này.

Không có cơ chế nào đảm bảo mọi family bất kỳ hỗ trợ mọi mặt phẳng uốn/roll; các trường hợp routing không hỗ trợ phải thất bại nguyên tử với thông báo rõ, không được tự đổi kích thước, đổi hệ hoặc để lại duct rời.

Khôi phục source nếu cần: đối chiếu hash hiện tại trước, phục hồi **chỉ** hai helper mỗi target từ `baseline/` và bỏ ba module mới mỗi target của dispatch này; giữ thay đổi mới của người khác. Chưa thực hiện khôi phục. Artifact build trong task chưa được cài/triển khai.
