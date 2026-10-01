# Cẩm nang kỹ thuật BIN - MEP

> Phiên bản tài liệu: 18/09/2026 · Phạm vi: các lệnh được `Panel.cs` đăng ký trên tab **BIN - MEP** (runtime `src/net8.0-windows`).

## Mục lục

- [Nguyên tắc vận hành chung](#nguyên-tắc-vận-hành-chung)
- [Tab 1 — BIM - MEP](#tab-1--bim---mep)
  - [Panel 1 — Micro Tools](#panel-1--micro-tools)
  - [Panel 2 — Sprinkler Tools](#panel-2--sprinkler-tools)
  - [Panel 3 — Drainage Tools](#panel-3--drainage-tools)
  - [Panel 4 — Support Tools](#panel-4--support-tools)
  - [Panel 5 — MEP Tools](#panel-5--mep-tools)
  - [Panel 6 — Check Tools](#panel-6--check-tools)
- [Tab 2 — BIM - DOCS](#tab-2--bim---docs)
  - [Panel 7 — BOQ & Excel Tools](#panel-7--boq--excel-tools)
  - [Panel 8 — Sheet & View Tools](#panel-8--sheet--view-tools)
  - [Panel 9 — 2D Annotation Tools](#panel-9--2d-annotation-tools)
  - [Panel 10 — Transfer & Link Tools](#panel-10--transfer--link-tools)
- [Ma trận nghiệm thu chung](#ma-trận-nghiệm-thu-chung)

## Nguyên tắc vận hành chung

### Cách đọc workflow

| Ký hiệu | Ý nghĩa thực tế trong mã |
|---|---|
| **Pre-selection** | Chọn đối tượng trước khi bấm Ribbon. Chỉ dùng khi lệnh đọc `Selection.GetElementIds()`; không tự suy luận rằng mọi lệnh có hỗ trợ. |
| **Pick / Click** | Bấm nút rồi chọn theo prompt trên Status Bar. `Esc` hủy lượt chọn/lệnh. |
| **Quét + Finish** | `PickObjects`/rectangle: quét chọn, bấm **Finish** trên Options Bar để xác nhận tập; `Esc` hủy. |
| **Loop** | Lệnh tiếp tục nhận cặp/điểm mới; `Esc` mới kết thúc vòng lặp. |
| **Hộp cấu hình** | WPF/TaskDialog xuất hiện trước khi pick; `Cancel`/đóng hộp không tạo hình học. |

> [!WARNING]
> Các lệnh tạo fitting phụ thuộc Routing Preferences, Pipe/Duct Type, System Type, kích thước và connector cùng domain. API `NewElbowFitting`/`NewTeeFitting` có thể thất bại dù hai đầu nhìn có vẻ chạm nhau. Không dùng kết quả tạo fitting như bằng chứng đã đúng topology: luôn kiểm tra connector, System Inspector và clash sau lệnh.

> [!TIP]
> Trước thao tác thay đổi topology, lưu file/Sync, làm thử trong 3D view với Detail Level Fine và chỉ hiện hệ liên quan. Sau mỗi lệnh: kiểm tra đầu hở, cao độ giữa (Middle Elevation), diameter, System Type và các fitting vừa tạo.

### Chuẩn tiền kiểm áp dụng cho mọi lệnh tạo/chỉnh MEP

1. Đối tượng có `LocationCurve` thẳng nếu lệnh yêu cầu cắt/extend; connector là End connector hợp lệ, không bị khóa/đã nối sai.
2. Type chứa cỡ và fitting mong muốn trong Routing Preferences; Pipe/Duct cùng domain và system type tương thích.
3. Đơn vị hộp BIN thường là **mm**, nhưng Revit API nội bộ là feet; nhập/đọc tham số hãy xác nhận đơn vị project.
4. Family support/sprinkler phải có connector đúng domain và hướng; family face/level-host phải hợp lệ với cách đặt.
5. Với lệnh quét, chỉ giữ đối tượng đúng scope. Hủy bằng `Esc`, không dùng Undo để thoát pick mode.

## Panel 1 — Micro Tools

### Danh mục và test nhanh

| Nút Ribbon · Class | Mục đích / workflow chuẩn | Happy path | Biên / lỗi cần thử |
|---|---|---|---|
| Move Connect · `MoveConnectCmd` | Pick đích giữ nguyên, rồi phần tử cần dời; preselect có thể dùng làm đích. Di chuyển phần tử thứ hai đến connector gần nhất và nối. | Hai pipe/duct/fitting có đầu hở cùng domain. | Connector đã nối, connector lệch tâm, family nhiều connector. |
| Disconnect · `DisconnectCmd` | Pick hai MEP element đang nối; API dò `AllRefs` và ngắt cặp. | Pipe–elbow đang kết nối. | Hai phần tử chỉ giao hình học, không có quan hệ connector. |
| Trim 3D · `Trim3DCmd` | Chọn Option 1/2/3 trong TaskDialog, rồi pick theo prompt. | Hai pipe tròn hoặc hai duct tròn thẳng cùng loại, lệch Z. | Duct chữ nhật, đồng cao độ, song song/lệch XY, không có tee/elbow routing. |
| Elbow Up/Down/Left/Right · `Elbow*Cmd` | Pick điểm gần đầu hở của Pipe/Duct/Conduit/Cable Tray; tạo extension + elbow theo hệ trục connector. | Ống ngang, một đầu hở. | Ống đứng/xiên; đầu được click đã nối; type thiếu elbow 90°. |
| Elbow Up/Down/Left/Right 45 · `Elbow*45Cmd` | Như trên, endpoint tính theo vector 45°. | Pipe ngang, type có 45°. | Routing không cho 45°, vector connector gần song song trục Z. |
| Rotate · `RotateElementsCmd` | Preselect hoặc pick phần tử; pick trục pipe/fitting; chọn góc trong cửa sổ. | Một elbow/fitting không cần giữ liên kết. | Đang nối nhiều nhánh; trục không có LocationCurve hợp lệ. |
| Rotate Multi · `RotateMultiCmd` | Preselect hoặc quét nhiều cút trên các tuyến song song, rồi nhập góc. Finish xác nhận quét. | Cùng loại fitting, mỗi cút nhận diện được tuyến trục. | Lẫn cút khác topology; không tìm được trục liên kết. |
| 3D Align · `ThreeDAlignCmd` | Pick phần tử đích, rồi phần tử di chuyển; căn connector trống gần nhất và nối. | Hai đầu hở cùng domain. | Đích không có connector trống/khác kích thước. |
| Branch Lite · `BranchAlignLiteCmd` | Pick main tại điểm cần lấy chuẩn, pick branch. | Branch thẳng cần khớp cao độ/hướng main. | Main/branch khác domain, nhiều connector mơ hồ. |
| Align Elev · `AlignPipeElevationCmd` | Pick pipe chuẩn, tiếp pick pipe đích (hoặc hủy); cân Middle Elevation. | Hai pipe thẳng ngang. | Pipe xiên, location không hợp lệ, offset gây va chạm. |
| Delete System · `DeleteSystemCmd` | Preselect một Pipe/Duct hoặc pick một; xóa mạng system liên thông. | Mạng test độc lập. | Mạng lớn/linked/đang workshare. |
| Delete Orphan Insu · `DeleteOrphanInsuCmd` | Chọn Active View hoặc Entire Project trong TaskDialog. | Insulation host đã mất. | Insulation hợp lệ nhưng host bị ẩn/temporary view. |

### Move Connect và Disconnect

**Mục đích.** `MoveConnectCmd` giải bài toán kéo một đoạn/fitting đến đúng đầu còn lại, chuẩn hóa tọa độ connector và gọi kết nối; `DisconnectCmd` gỡ đúng liên kết connector giữa hai phần tử, không chỉ xóa hình học. Phù hợp khi sửa một điểm nối sau dựng layout.

**Điều kiện.** Cả hai phần tử phải là MEP element được filter, có ConnectorManager. Với Move Connect, cặp connector gần nhất phải hợp lệ và còn hở. Pre-selection của Move Connect được mã xem là thao tác có sẵn, nhưng vẫn cần theo prompt chọn phần tử còn lại; Disconnect là pick hai phần tử.

**Bẫy và khắc phục.** Không dùng Move Connect để “ép” hai system/domain khác nhau hay để kéo một fitting đã nối nhiều nhánh—tách nhánh trước. Nếu báo không có connector phù hợp/đã kết nối, dùng Disconnect, kiểm tra `AllRefs`, rồi pick sát đầu mong muốn. `Esc` tại bất kỳ pick nào hủy lệnh.

### Trim 3D

**Nút / class:** **Trim 3D** · `Trim3DCmd`. Lệnh có ba phương án: (1) đầu–đầu qua ống đứng và 2 elbow; (2) nhánh 90° → ống đứng → tee vào main; (3) nhánh 45° → đoạn xiên 45° → tee vào main. Đây là lệnh Interactive Pick, không đọc pre-selection.

**Workflow.** Bấm nút → chọn Option trong TaskDialog (Cancel để bỏ) → pick theo thứ tự hiện trên prompt. Option 1 chọn hai ống; Option 2/3 chọn nhánh rồi main. Lệnh trim/extend, tạo riser/diagonal, break main và tạo elbow/tee. Không có Space/Finish; `Esc` hủy pick.

**Prerequisite và giới hạn.** Chỉ nhận Pipe hoặc **duct tròn**, hai đối tượng cùng loại; duct chữ nhật bị mã từ chối. Cả hai phải là đoạn thẳng. Option 1 yêu cầu chênh Z, và với hai ống song song đòi XY trùng đủ để có riser đứng; Option 2/3 yêu cầu hình chiếu XY giao nhau và chênh Z đáng kể. Phải có elbow/tee phù hợp trong routing.

**Khắc phục.** Với 0°/180° hoặc hai line song song, dùng Offset/Turn thay vì Trim 3D. Với connector lệch tâm hoặc cỡ khác, chuẩn hóa diameter/type trước; nếu tee thất bại, kiểm tra main đã break thành hai nửa và routing preference có tee cùng cỡ. Test cả “nhánh trên main”, “nhánh dưới main”, chênh Z nhỏ sát tolerance, và duct round/rectangular.

### Elbow 90° và 45°

Tám nút dùng chung `ElbowHelper`: **Elbow Up/Down/Left/Right** (`ElbowUpCmd`, `ElbowDownCmd`, `ElbowLeftCmd`, `ElbowRightCmd`) và **Up/Down/Left/Right 45** (`ElbowUp45Cmd`, `ElbowDown45Cmd`, `ElbowLeft45Cmd`, `ElbowRight45Cmd`). Mã pick **một điểm trên** MEP curve, lấy end connector hở gần nhất và hướng mới theo coordinate system connector; nó không phải lệnh preselect/continuous draw.

**Dùng đúng.** Click gần đúng đầu hở, đặc biệt với đoạn ngắn. Đầu đã nối sẽ báo lỗi. 90° tạo đoạn đổi hướng theo Up/Down/Left/Right tương đối với hướng connector; 45° tạo vector chéo và yêu cầu fitting 45° có thể tạo. `Esc` hủy.

> [!NOTE]
> “Left/Right” là trái/phải theo basis của connector, không mặc định là trái/phải màn hình. Với ống đứng hoặc xiên, kết quả phụ thuộc hệ trục local; luôn thử một đối tượng mẫu trước khi áp dụng hàng loạt.

### Rotate, Rotate Multi và Align

`RotateElementsCmd` hỗ trợ preselected ID hoặc pick cút/phụ kiện/ống, sau đó pick pipe/fitting làm trục và chọn góc trong cửa sổ. `RotateMultiCmd` dùng preselection hoặc `PickObjects`; quét xong phải **Finish**, sau đó hộp nhập góc. Cả hai xác định trục từ MEP curve/fitting connector, vì vậy không phù hợp với family không connector hoặc topology nhiều nhánh cần giữ nguyên.

`ThreeDAlignCmd`, `BranchAlignLiteCmd`, `AlignPipeElevationCmd` là các lệnh pick tuần tự. 3D Align chọn đích trước; Branch Lite chọn main rồi branch; Align Elev chọn pipe chuẩn trước và pipe đích sau. Chúng phục vụ căn tâm/cao độ, không tự tránh clash hoặc tự thay routing. Test mốc: cùng Z, lệch Z nhỏ, pipe xiên, và tình huống không còn đầu hở. Sau chạy phải rà lại slope và clearance.

### Delete Tools

`DeleteSystemCmd` có preselection (nếu không có thì pick Pipe/Duct), sau đó xóa toàn bộ mạng liên thông của system. Đây là thao tác phá hủy phạm vi lớn: thử trên copy/workset riêng, kiểm tra selection/review trước Commit và `Undo` ngay nếu sai.

`DeleteOrphanInsuCmd` không pick host: mở TaskDialog để chọn **Active View** hay **Entire Project**. Nó chỉ xóa insulation nhận diện là không còn host. Chạy Active View trước; chỉ chạy toàn dự án sau khi backup/sync và kiểm tra filter/view không che host hợp lệ.

## Panel 2 — Sprinkler Tools

### Danh mục, cách chọn và test matrix

| Nút · Class | Workflow / mục đích | Happy path | Edge/pitfall chính |
|---|---|---|---|
| Sprinkler Flex Pipe · `ConnectSprinklerFlexPipeCmd` | Preselect 1 pipe + ≥1 sprinkler là one-shot; hoặc pick Pipe/Sprinkler theo cặp, lặp đến Esc. | Pendant có connector piping, branch pipe thẳng. | Preselect >1 pipe chuyển logic Multi; family không phải sprinkler/connector sai. |
| Sprinkler Flex Multi · `ConnectSprinklerFlexPipeMultiCmd` | Preselect pipe + sprinkler: 1 pipe nối mọi đầu, nhiều pipe ghép cặp; không preselect: quét cụm → **Finish**, lặp, Esc kết thúc. | Một cụm có pipe/sprinkler gần nhau. | Ghép nearest sai khi bố trí đối xứng; xem kết quả từng cặp. |
| Sprinkler To Pipe · `ConnectSprinklerToPipe` | Chọn type/offset/diameter trong WPF, quét sprinkler → Finish, pick main. | Pendant hoặc upright đúng type. | Một số type có thể dịch sprinkler theo mã; tránh chạy trực tiếp bản phát hành. |
| Sprinkler Flipper · `SprinklerFlipper` | Hộp chọn/lấy family, flip hướng Up/Down. | Family có orientation/connector chuẩn. | Host/offset/rotation không tương thích. |
| Shorten Sprinkler · `SprinklerShortenerVerticalPipe` | Chọn theo cửa sổ và đối tượng yêu cầu; rút ống đứng sprinkler. | Drop thẳng có đoạn thừa. | Drop xiên, fitting đầu mút đã khóa. |
| Reducing Elbow · `TransferToReducingElbow` | Chuyển topology sang reducing elbow. | Elbow giảm cỡ đúng routing. | Reducer/elbow không tồn tại theo cỡ. |
| Tee/Reducer Groove · `ConnectTeeAndReducer_Groove` | Chọn cấu hình, quét các pipe cần xóa → **Finish**; xử lý flange/tee/reducer. | Cụm grooved có flange và fitting đúng. | Không đúng số connector/cấu trúc flange. |
| Optimize Pipes · `OptimizePipeSegments` | Tối ưu các đoạn pipe liên tiếp theo UI/selection. | Chuỗi collinear ngắn. | Đoạn có branch/slope/fitting đặc biệt. |
| Remove Coupling · `RemovePipeCoupling` | Thay/loại coupling giữa đoạn pipe. | Coupling hai đầu hợp lệ. | Coupling không phải family/connector 2 đầu. |
| Replace Pipe With Nipple · `ReplacePipeWithNipple` | Thay đoạn đã chọn bằng nipple/fitting. | Đoạn ngắn giữa fittings. | Length âm/thiếu nipple type. |
| Check Sprinkler · `CheckSprinklerCmd` | Mở modeless checker qua ExternalEvent; lưu resolved IDs. | Quét, chọn kết quả, đánh resolved. | Không đóng WPF khi đổi document; refresh sau thay đổi. |

### Kết nối Flex Pipe: khác biệt quan trọng giữa hai nút

**Sprinkler Flex Pipe** (`ConnectSprinklerFlexPipeCmd`) đọc preselection trước. Nếu có đúng **1 Pipe + một hay nhiều Sprinkler**, nó kết nối một lượt và xóa selection; không vào pick mode. Nếu có nhiều pipe + sprinkler, mã gọi logic Multi. Không preselect: pick đối tượng đầu (pipe hoặc sprinkler), pick đối tượng còn lại, tạo một cặp và lặp; `Esc` kết thúc, các cặp đã thành công vẫn được giữ.

**Sprinkler Flex Multi** (`ConnectSprinklerFlexPipeMultiCmd`) dành cho cluster. Có preselection thì không hiện quét; một pipe cấp cho nhiều sprinkler, còn nhiều pipe dùng pairing theo khoảng cách. Không preselection: quét một cụm, bấm **Finish** để kết nối cụm đó, rồi lặp cụm khác; `Esc` kết thúc toàn lệnh.

**Tiền kiểm.** Family phải được nhận diện là sprinkler và có connector piping dùng được; pipe thuộc piping domain, routing preference có flex/branch/elbow/reducer cần thiết. Test happy path 1×1, 1×n, n×n; test pipe/sprinkler có khoảng cách bằng nhau (rủi ro pairing), đầu đã nối và size reducer. Sau lệnh, kiểm tra từng flex/branch, độ dài tối thiểu và không tự giao.

> [!WARNING]
> Không dùng Multi trên sơ đồ đối xứng nếu chưa kiểm soát pairing: mã ghép theo hình học gần nhất, không theo tag/line number. Chia cluster nhỏ hoặc dùng preselection từng cụm để tránh nối chéo.

### Sprinkler To Pipe, Flipper và thao tác topology

`ConnectSprinklerToPipe` mở WPF chọn 7 kiểu pendant/upright, offset Z và diameter; tiếp đó, các kiểu dùng `PickObjects` yêu cầu **Finish** để chốt sprinkler rồi pick main pipe. Mã có `TransactionGroup`, tạo drop, break main và tee; ít nhất một luồng pendant còn có thể hạ sprinkler xuống dưới main trước khi tạo. Vì vậy kiểm tra cao độ/host của sprinkler trước và sau chạy, không chạy lệnh này để “chỉ kiểm tra”.

`SprinklerFlipper`, `SprinklerShortenerVerticalPipe`, `TransferToReducingElbow`, `OptimizePipeSegments`, `RemovePipeCoupling`, `ReplacePipeWithNipple` đều là lệnh biến đổi topology/instance. Chuẩn thao tác là đọc hộp cấu hình nếu có, chọn đúng scope theo prompt, rồi kiểm tra System/connector. Rủi ro phổ biến: pipe cỡ không có reducing elbow/nipple, coupling không phải 2-port, segment có branch hoặc slope. Khắc phục bằng cách phục hồi segment trước, chuẩn hóa type/routing, làm trên một mẫu và kiểm tra result trước batch.

`ConnectTeeAndReducer_Groove` là lệnh chuyên dùng cụm grooved: hộp chọn trước, quét các đoạn pipe cần loại bỏ và bấm **Finish**. Mã dò flange/fitting connector nên không nên dùng cho piping hàn/rãnh không theo cấu trúc flange mong đợi. Test đủ: ống đúng tee/reducer/flange, đảo chiều chọn, thiếu flange, và fitting 3/2 connector không đúng kỳ vọng.

### Check Sprinkler

**Check Sprinkler · `CheckSprinklerCmd`** chỉ mở/activate một cửa sổ modeless `CheckSprinklerWindow` qua `ExternalEvent`; kết quả resolved được load từ storage trong document. Đây là công cụ kiểm tra, không phải lệnh chọn trực tiếp Ribbon. Đóng cửa sổ khi chuyển document; sau khi sửa model, thực hiện refresh/scan trong UI rồi mới resolve. Test: sprinkler hợp lệ, thiếu kết nối, đã resolved, mở nút lần hai (phải activate cửa sổ hiện có).

## Panel 3 — Drainage Tools

| Nút · Class | Mục đích / workflow | Prerequisite và test | Bẫy / xử lý |
|---|---|---|---|
| Draw Drainage · `DrawDrainagePipe_AllTypes` | Mở WPF chọn loại/config rồi gọi workflow vẽ tương ứng. | Pipe Type/system/level drainage có sẵn; test từng type. | Cancel WPF không tạo gì; xác minh slope, direction flow và routing sau vẽ. |
| Create Elbows 45 · `Create2Elbows45` | WPF cấu hình → pick pipe 1, pipe 2; Esc quay lại/hủy. | Hai pipe thẳng tương thích, có 45° routing. | Đường thẳng song song/đồng tuyến, cỡ khác, break point ngoài curve. |
| Join 2 Elbows · `Join2Elbows` | Xác nhận dialog, pick đoạn pipe nằm giữa hai elbow. | Đoạn trung gian thực sự nối đủ 2 elbow. | Pick nhầm đoạn / elbow thiếu đầu hở; lệnh báo không tìm được pipe phía sau. |
| Connect Vent Riser · `ConnectVentRiser` | TaskDialog hướng dẫn → pick vent riser, rồi main. | Hai pipe thẳng có thể break, tee phù hợp. | Gần đồng cao độ/không có giao điểm, tee routing thiếu. |
| Turn Drainage · `TurnDrainagePipe` | WPF chọn thông số, pick điểm trên pipe để xác định đầu quay. | Pipe drainage thẳng có đầu hở. | Chọn giữa pipe tạo đầu quay không mong muốn; slope/flow bị đảo. |
| Draw Clean Out · `DrawCleanOut` | WPF, quét pipe → **Finish**, tạo cleanout dựa connector used/unused. | Pipe có đầu còn trống / family cleanout đúng connector. | Pipe không có used + unused connector như kỳ vọng, family không cùng domain. |

### Hướng dẫn kiểm tra thoát nước

Các lệnh drainage là interactive; trừ quy trình quét Clean Out, không dựa vào preselection trong code. `Create2Elbows45` có 2 lượt pick; `Join2Elbows` yêu cầu chọn **đoạn giữa** hai elbow chứ không phải elbow; `ConnectVentRiser` chọn vent trước/main sau. `DrawCleanOut` quét rồi bấm **Finish**.

**Mục đích thực tế.** Các lệnh giảm thao tác cắt–nối thủ công ở nhánh thông hơi, offset 2×45 và cửa thăm; đồng thời cố gắng giữ fitting đúng topology. Tuy nhiên, BIM drainage còn phải kiểm tra slope/flow. Sau mỗi lệnh, dùng slope arrow/System Inspector hoặc tham số pipe để xác nhận hướng chảy không bị đảo.

> [!NOTE]
> API break pipe chỉ hợp lệ khi điểm break nằm trên `LocationCurve`. Nếu geometry gần giao nhưng không thật sự giao (sai Z hoặc sai XY), hãy trim/extend có kiểm soát trước thay vì cố tạo tee/2 elbow.

## Panel 4 — Support Tools

| Nút · Class | Workflow chuẩn | Tiền đề / Happy path | Edge cases và khắc phục |
|---|---|---|---|
| Place Support · `PlaceSupport` | WPF chọn loại/tham số → pick Revit link kết cấu → quét MEP theo loại filter, **Finish**. | Link structure loaded; support family/type và tham số được yêu cầu tồn tại; pipe/duct/tray/conduit phù hợp. | Link unload, transform link, pipe đứng, family thiếu parameter. Kiểm tra host/elevation sau đặt. |
| Multi Pipe Support · `PlaceMultiPipeSupport` | WPF → pick link kết cấu → quét các Pipe → **Finish**. | Nhiều pipe song song, cùng cao độ/sắp xếp hợp lý; family có `SPACE U BOLT n`. | Số pipe vượt slot family, thứ tự/mép trái thay đổi, pipe không song song. |
| Align Support · `AlignSupportCmd` | Mở modeless AlignSupportWindow dùng ExternalEvent; thao tác theo UI. | Support và MEP đã tồn tại, family cho phép dịch. | Window không tự là selection; refresh UI sau edit/topology. |
| Move Support · `MoveSupportCmd` | Pick support, rồi pick **một điểm trên** Pipe/Duct/Cable Tray; điểm được project về curve. | Support có Location; curve thẳng/hợp lệ. | Point ngoài segment, support bị host/constraint, curve không project được. |

**Mục đích.** Place Support/Multi giảm thời gian đặt treo theo link kết cấu, trong khi Align/Move tinh chỉnh support về tâm tuyến/điểm projection. Đối với Multi, mã ghi spacing bằng tham số tên `SPACE U BOLT {i}`; đây là hợp đồng family cụ thể, không phải chuẩn Revit chung.

**Quy trình an toàn.** Đặt thử một support, kiểm Level/Z, offset, hướng rod và tọa độ host trong link. Chỉ sau đó quét theo nhóm. Với Move Support, click thứ hai phải nằm trên curve mục tiêu; lệnh project raw point nên hãy kiểm tra lại vị trí thực sau commit. Không có Space; Finish chỉ dùng ở các lệnh quét, `Esc` hủy.

## Panel 5 — MEP Tools

### Danh mục chính

| Nút · Class | Cách dùng / giá trị | Test & hạn chế |
|---|---|---|
| Avoid Clash · `AvoidClashCmd` | Pick pipe cần uốn, pick obstacle; WPF chọn tham số; tạo offset 4 cút né va chạm. | Pipe thẳng có đủ chiều dài, clearance đủ; test obstacle từng phía, clearance nhỏ, routing thiếu elbow. |
| Pipe Insulation · `PipeInsulationCmd` | WPF chọn Insulation Type, rule/thickness, remove existing và scope selection/Active View. | Project phải có Pipe Insulation Type; test pipe, fitting, size match/no-match. |
| Turn MEP · `TurnMEP` | WPF chọn Pipe/Duct/Cable Tray, hướng/góc/offset; pick đối tượng và điểm quay. | Type có fitting và curve thẳng; test 45/90, đầu hở/đã nối. |
| MEP Offset · `MEPOffset` | WPF cấu hình offset, pick theo filter. | Test XY/Z, độ dài tối thiểu, elbow routing. |
| Cut MEP By Level · `CutMEPByLevel` | Hộp chọn level/config, cắt MEP theo cao độ. | Test crossing level, curve không cắt level, slope. |
| Bloom · `BloomCmd` | Tạo pipe từ fitting/connector theo workflow tool. | Fitting có connector piping hở; test 2-port/nhiều port. |
| Split Duct · `SplitDuctCmd` | Chọn/cấu hình để split duct. | Duct thẳng; test round và rectangular theo UI/routing. |
| Place Family · `PlaceFamily` | Pick CAD link, pick Family mẫu; WPF chọn CAD block/family type/level/elevation; place theo block. | CADLink và block có insertion points; test duplicate XY, level/elevation, transform. |
| Draw Multi Pipe · `DrawMultiPipe` | Quét pipe rectangle, click nhiều điểm; trong vùng thì extend, ngoài vùng chọn 45/90 rồi tạo turn song song. | Pipe song song/cùng layout; Finish không dùng, Esc kết thúc pick. |
| Clash Detective · `ClashDetectiveCmd` | Mở checker/UI qua ExternalEvent. | Scan, select, resolve; refresh sau chỉnh model. |
| Select By Parameter · `SelectByParameterCommand` | UI lọc/chọn phần tử theo parameter. | Test parameter missing, shared/built-in, giá trị trùng. |

### Avoid Clash: giới hạn hình học

**Avoid Clash · `AvoidClashCmd`** là lệnh pick pipe trước, obstacle sau, rồi WPF cấu hình. Mục tiêu là tạo đoạn offset với **4 elbow** để né vật cản. Nó không phải Clash Detective tổng quát và không giải xung đột mạng nhiều nhánh. Cần pipe thẳng đủ khoảng lùi trước/sau vùng va chạm, cỡ/type có elbow. Test rõ bốn hướng, obstacle tại đầu pipe, clearance yêu cầu lớn hơn chiều dài hiện có, pipe có insulation và obstacle dạng link/element khác. Nếu thất bại, kiểm routing preference và làm offset bằng Turn/MEP Offset có kiểm soát.

### Insulation

`PipeInsulationCmd` lấy toàn bộ Pipe Insulation Type của project. WPF cho phép chọn type, rule độ dày, xóa insulation cũ và scope: **Apply to all** thu thập Pipe/Pipe Fitting trong Active View; nếu không, lệnh dùng **pre-selection** Pipe/Fitting và sẽ hủy nếu selection rỗng. API tạo insulation riêng cho pipe và fitting, đếm created/skipped/no-match.

**Bẫy.** Chạy Remove Existing có tính phá hủy lớp cũ trong scope. Size không match rule được báo no-match; element đã có insulation hoặc API lỗi bị skip. Hãy test DN nhỏ/lớn, fitting, không có type và active view bị crop. Sau chạy kiểm tra thickness bằng đơn vị project và clearance clash (insulation làm tăng bao hình học).

### Turn / Offset / Cut / Bloom / Split Duct

`TurnMEP` mở WPF trước, chọn loại Pipe/Duct/Cable Tray, hướng, góc và offset, sau đó pick một đối tượng và vị trí quay. `MEPOffset` và `CutMEPByLevel` cũng là các lệnh cấu hình-hình học: chỉ vận hành trên đối tượng đúng filter/curve, cần kiểm mối nối và elevation sau thay đổi. `BloomCmd` xuất phát từ fitting/connector để triển khai pipe; `SplitDuctCmd` làm split duct. Không có bằng chứng trong Ribbon rằng các lệnh này hỗ trợ preselection, vì vậy dùng theo UI/prompt thay vì quét sẵn.

**Checklist edge chung:** đoạn xiên; point sát đầu curve; fitting đã nối; type thiếu elbow/transition; system type khác; offset tạo self-clash; cắt đúng tại Level và cắt ngay sát connector. Nếu tự động fitting lỗi, hủy transaction/Undo rồi chuẩn hóa curve/type trước khi thử lại.

### Place Family từ CAD block

`PlaceFamily` là Interactive Pick: pick **CAD Link**, pick một **FamilyInstance mẫu**, sau đó WPF cho chọn block CAD, family/type, Level và elevation. Code đọc insertion point block, khử trùng theo XY (tolerance 1 mm), bỏ vị trí đã có cùng symbol trên level; nó đặt `XYZ(x, y, level.Elevation + elevation)`, cố set Free Host Offset/Elevation, và chọn các instance vừa tạo.

**Điều kiện.** CAD phải là link và block phải đọc được; FamilySymbol/type đã load; level được chọn tồn tại; family phải có placement type hỗ trợ tạo tại point/level. Test DWG Z=0 ở tầng trên, CAD transform, block trùng XY, vị trí đã có family, và family không cho set elevation. Sau chạy, kiểm **Level, Z, XY**, selection và Selection Box/diagnostic thay vì chỉ tin số lượng thông báo.

> [!TIP]
> Khi phần tử vừa đặt “không thấy”, không tìm ID thủ công: giữ selection của lệnh, gọi **Selection Box** trong Revit và so sánh Level/Z thực. Diagnostic lệnh ghi trạng thái placement; lỗi thường là CAD Z/transform hoặc view range, không phải không tạo family.

### Draw Multi Pipe, Clash Detective, Select By Parameter

`DrawMultiPipe` hiện hướng dẫn, cho quét rectangle các Pipe; sau đó lặp `PickPoint`. Click trong vùng đã chọn thì extend; click ngoài vùng thì chọn **45 Degrees/90 Degrees** trong TaskDialog và tạo các turn song song. `Esc` kết thúc vòng pick. Điều kiện tốt nhất là các pipe song song, cùng pattern; test khoảng cách không đều, click đúng/ngoài vùng, click trùng projection và 45/90.

`ClashDetectiveCmd` và `SelectByParameterCommand` là công cụ UI/kiểm tra: mở UI rồi scan/filter/select theo thao tác cửa sổ. Test phải bao gồm parameter không tồn tại, giá trị rỗng/trùng và danh sách kết quả sau khi model thay đổi. Không coi select result là clash đã được sửa; sau sửa phải scan lại.

### Các nút bổ sung đã đăng ký trong Panel BIN - MEP

Ribbon hiện còn đăng ký `RotateFamilyInstance` (**Rotate Family**), `ChangeReferenceLevel` (**Change Ref Level**), `ToggleInsulation` (**Toggle Insulation**), `SelectElement` (**Manage Element**) và `CompareElementCommand` (**Compare Element**). Chúng không nằm trong danh mục người dùng yêu cầu nhưng vẫn thuộc source của tab. Vận hành chúng theo hộp UI/prompt và áp dụng tiền kiểm chung: family/level/reference hợp lệ, parameter có thể ghi, và chỉ so sánh/quản lý trong document hiện hành. Với Change Ref Level/Rotate Family, kiểm tra host, elevation/offset, orientation và connector sau thay đổi; với Toggle Insulation, phân biệt visibility với việc tạo/xóa insulation.

## Panel 6 — Check Tools

| Nút · Class | Chức năng / workflow | Ca kiểm thử và lưu ý |
|---|---|---|
| Check Connected Pipe · `CheckConnectedPipeCmd` | Mở modeless checker, storage resolved pipe. | Pipe liên thông, đầu hở, đã resolved; refresh sau sửa. |
| Check Undefined Pipe · `CheckUndefinedPipeCmd` | Mở modeless checker, phát hiện và kiểm tra các ống chưa xác định hệ thống (Undefined/Unassigned System). | Quét tìm ống chưa gán SystemType, lưu trạng thái resolved vào Document Extensible Storage. |
| Check Pipe Fitting · `CheckPipeFittingCmd` | Mở checker modeless qua ExternalEvent. | Fitting hợp lệ/lỗi, resolved persistence, reopen window. |
| Pipe Clash Clearance · `CheckPipeClashCmd` | Preselect 2–3 Pipe, hoặc quét pipe rồi **Finish**; chọn mode/required clearance WPF; có thể Zoom selected. | 2 pipe clear, 2 pipe clash, 3 pipe, insulation thickness và <2/>3 selection. |
| Check Air Terminal · `CheckAirTerminalCmd` | Mở checker modeless qua ExternalEvent. | Terminal có/không có kết nối, resolved storage. |
| Flex Duct Avoid MEP · `FlexDuctAvoidMepCmd` | WPF → pick obstacle → pick flex duct; Esc quay lại/hủy. | Flex duct đủ control geometry; không clash, clearance đạt/chưa đạt, obstacle filter. |
| Get ID · `GetIdCmd` | Nếu preselected dùng selection; nếu không pick element hoặc element trong link. | Host element, linked element, workset/type/id output. |

### Connected Pipe / Undefined Pipe / Pipe Fitting / Air Terminal

Bốn checker `CheckConnectedPipeCmd`, `CheckUndefinedPipeCmd`, `CheckPipeFittingCmd`, `CheckAirTerminalCmd` mở cửa sổ modeless singleton bằng `ExternalEvent`, và load các ID đã resolved từ Extensible Storage của document. Quy trình: mở cửa sổ → scan/filter trong UI → chọn/zoom result → sửa topology → rescan → đánh resolved khi đã xác nhận. Bấm Ribbon lần hai chỉ activate cửa sổ hiện hữu. `Esc` không phải cơ chế chính của modeless window; đóng cửa sổ bằng UI.

### Pipe Clash Clearance, Flex Duct Avoid MEP và Get ID

`CheckPipeClashCmd` ưu tiên **preselection** 2 hoặc 3 Pipe; nếu ít hơn 2, nó yêu cầu quét pipe, bấm **Finish**, và chỉ lấy tối đa 3. WPF cho chọn mode và khoảng hở yêu cầu, dùng cả thickness insulation qua `PipeInsulationRules`; có tùy chọn zoom kết quả. Nó là kiểm tra local 2–3 pipe, không thay Clash Detective toàn model.

`FlexDuctAvoidMepCmd` mở WPF, pick obstacle rồi flex duct. Nếu không có clash hoặc khoảng cách đã đủ, lệnh thông báo; không cần làm thêm. Kiểm tra control point/độ dài flex sau đổi để tránh radius phi thực tế. `GetIdCmd` hỗ trợ preselection và `ObjectType.LinkedElement`: dùng để lấy ID đối tượng host/link, phục vụ truy vết, không chỉnh model.

## Tab 2 — BIM - DOCS

### Panel 7 — BOQ & Excel Tools

| Nút · Class | Chức năng / workflow | Ca kiểm thử và lưu ý |
|---|---|---|
| BOQ · `BOQCommand` | Thống kê số lượng, chiều dài, diện tích của các hệ thống MEP theo Type/System/Level; xuất ra bảng tổng hợp và file Excel. | Kiểm tra các family có tham số chiều dài/kích thước, pipe/duct fitting, phân loại đúng theo hệ thống. |
| Schedule To Excel · `ExportScheduleToExcel` | Mở hộp thoại chọn bảng Schedule có sẵn trong dự án và xuất trực tiếp ra tệp tin định dạng `.xlsx`. | Schedule có gom nhóm (group header), schedule nhiều cột, ký tự tiếng Việt Unicode trong tiêu đề. |

### Panel 8 — Sheet & View Tools

| Nút · Class | Chức năng / workflow | Ca kiểm thử và lưu ý |
|---|---|---|
| Sheet From Excel · `SheetFromExcel` | Đọc file Excel cấu hình (Sheet Number, Sheet Name) và tự động tạo hàng loạt Sheet mới trong dự án. | File Excel thiếu cột, trùng Sheet Number đã tồn tại, Title Block hợp lệ. |
| Copy Sheet · `CopySheet` | Nhân bản một Sheet đã có kèm theo Titleblock, chú thích 2D và tùy chọn nhân bản các View phụ thuộc. | Sheet có View schedule, legend, floor plan; kiểm tra View name trùng sau khi copy. |
| Copy View · `CopyView` | Sao chép nhân bản hàng loạt View đã chọn kèm toàn bộ thiết lập đồ họa (VG Overrides, Scale, Phase). | View 3D, Floor Plan, Section; kiểm tra duplicate with detailing vs duplicate thông thường. |
| Copy Filter · `CopyFilterCmd` | Chọn View nguồn mang các bộ lọc (View Filters) và áp dụng hàng loạt sang danh sách các View đích. | View đích đã có sẵn filter (cập nhật override) hoặc chưa có filter (thêm mới filter vào view). |

### Panel 9 — 2D Annotation Tools

| Nút · Class | Chức năng / workflow | Ca kiểm thử và lưu ý |
|---|---|---|
| Grid & Level · `ManageGridLevel` | Bật/tắt hàng loạt bong bóng trục Grid (Bubble) và đầu mút Level 2D trong khung nhìn hiện hành. | View bị crop region, grid bị ẩn, trục cong/trục xiên. |
| Section Head/Tail · `ToggleSectionHeadCmd` | Đảo chiều hoặc bật/tắt hiển thị ký hiệu đầu (Head) và đuôi (Tail) của các đường Section cắt ngang. | Section cắt nhiều hướng, section có nhiều segment gãy khúc. |
| Delete 2D Element · `Delete2DElement` | Quét dọn và xóa hàng loạt đối tượng 2D (Detail Lines, Text Notes, Dimensions rác) trong View đang chọn. | Tránh xóa nhầm Dimension gắn trên cấu kiện chính; thao tác có hộp xác nhận scope. |
| Rename 2D Element · `Rename2DElement` | Đổi tên hàng loạt View, Sheet hoặc Family Type bằng quy tắc thêm tiền tố (Prefix), hậu tố (Suffix) hoặc Tìm kiếm & Thay thế (Find & Replace). | Trùng tên sau khi đổi, ký tự không hợp lệ trong Revit (`\ / : * ? < > |`). |

### Panel 10 — Transfer & Link Tools

| Nút · Class | Chức năng / workflow | Ca kiểm thử và lưu ý |
|---|---|---|
| Export Family · `ExportFamily` | Xuất toàn bộ hoặc các Family đã chọn trong dự án ra thư mục cục bộ dạng `.rfa` kèm thanh tiến trình (progress bar). | Family hệ thống (System Family) sẽ được bỏ qua; family lồng (nested) và family read-only. |
| Transfer Document · `TransferDocument` | Sao chép đối tượng 2D/3D trực tiếp giữa hai tệp Revit Document đang cùng mở trong phiên làm việc. | Hai document khác đơn vị đo (Unit), khác Level hoặc thiếu Family Definition tương ứng. |
| Copy From Link · `CopyFromLinkCmd` | Pick chọn đối tượng từ file liên kết (Revit Link) và copy chuẩn xác về đúng tọa độ của mô hình chủ (Host Document). | Link có Transform góc xoay/dịch gốc tọa độ Shared Coordinates; element bị thay đổi geometry. |

## Ma trận nghiệm thu chung

| Nhóm | Happy path bắt buộc | Boundary bắt buộc | Tiêu chí pass |
|---|---|---|---|
| Tạo fitting | Cùng type/domain/cỡ, đầu hở, routing đầy đủ | 0°/180°, lệch Z nhỏ, cỡ khác, thiếu routing | Không tạo geometry rác; connector/system đúng sau tạo. |
| Cắt/offset/trim | Pipe/Duct thẳng, có chiều dài dự phòng | Xiên, song song, điểm ngoài curve, gần endpoint | Transaction rollback sạch khi fail; slope/elevation giữ đúng. |
| Batch/preselect | Tập đồng nhất 2–n đối tượng | Selection rỗng/lẫn category/số vượt family slot | Scope đúng; Finish/Esc hoạt động; báo partial rõ. |
| Family/support | Family/link/level/type hợp lệ | CAD/link transform, family thiếu parameter/host | Kiểm Level–Z–XY–host và selection của phần tử mới. |
| Checker | Lỗi thật, phần tử hợp lệ, resolved | Reopen window, đổi document, sửa rồi rescan | Không dùng cache stale; resolved có truy vết. |

## Kết luận vận hành

BIN TOOL là bộ công cụ toàn diện hỗ trợ tăng tốc mô hình hóa MEP và quản trị hồ sơ bản vẽ (BIM - DOCS), không thay thế kiểm soát thiết kế. Lệnh “thành công” nghĩa là transaction đã commit; nghiệm thu MEP chỉ hoàn tất khi hệ thống, connector, slope/elevation, fitting, insulation clearance và clash được kiểm lại trong model. Toàn bộ 75 lệnh trên 10 panel đã được tự động hóa kiểm định và đồng bộ hoàn chỉnh trên cả 2 runtime .NET 4.8 và .NET 8.0.
