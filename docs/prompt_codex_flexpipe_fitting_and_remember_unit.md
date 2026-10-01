# Yêu cầu Kỹ thuật cho Codex: Nâng cấp Ghi nhớ Đơn vị CAD & Khắc phục Lỗi Sprinkler Flex Pipe Khi Ống Đã Có Fitting Sẵn

## 1. Bối cảnh & Mục tiêu
Người dùng có 2 yêu cầu cụ thể cần hoàn thiện:
1. **Ghi nhớ lựa chọn CAD Unit trong Place Family:**
   - Trong `PlaceFamilyWindow.cs`, hàng `CAD Unit` đã được thêm thành công.
   - Tuy nhiên, mỗi lần mở hộp thoại thì ComboBox đang bị reset về index 0 (`Auto Detect`).
   - Cần bổ sung cơ chế **ghi nhớ lựa chọn trước đó** (lưu vào biến `static`) để khi làm dự án hệ Inch hoặc Millimeter, người dùng chọn 1 lần thì các lần sau mở tool lên sẽ tự động giữ nguyên lựa chọn đó mà không phải chọn lại.
2. **Khắc phục lỗi Sprinkler Flex Pipe khi ống đã có sẵn Fitting:**
   - Hiện tượng lỗi trong ảnh: *"Sprinkler 11178553: vị trí chiếu nằm ngoài thân ống hoặc quá sát đầu ống (< 100 mm)"*.
   - Nguyên nhân: Ống nhánh đã có sẵn phụ kiện Tê (Tee) hoặc Cút (Elbow) rẽ xuống ở đầu ống. Do ống đã cắm vào fitting nên `GetUnusedConnectors(pickedPipe)` trả về 0 đầu hở. Tool hiểu lầm ống nhánh là Ống chính (Main Pipe) và cố đục Tê giữa thân. Do Sprinkler nằm ngay dưới fitting ở đầu ống, điểm chiếu bị rơi ra ngoài đầu ống (< 100 mm) gây lỗi và bắt buộc người dùng phải xóa fitting thủ công.
   - Giải pháp: Tự động phát hiện Fitting có đầu chờ hở gắn ở đầu ống, lấy đầu chờ đó làm điểm nối trực tiếp (Direct Branch) và cho phép người dùng click chọn trực tiếp cả `PipeFitting`.

---

## 2. Chi tiết Kỹ thuật Cần Triển khai

### A. Ghi nhớ Lựa chọn trong `PlaceFamilyWindow.cs` (Cả `src/net8.0-windows/` và `src/net48/`)
1. Thêm biến `static` lưu lựa chọn gần nhất:
   ```csharp
   private static string LastSelectedCadUnit = "Auto Detect (Tự động)";
   private static bool LastCalibrateBasepoint = true;
   ```
2. Trong hàm khởi tạo / `InitializeComponent`:
   - Gán trạng thái từ biến static:
     ```csharp
     if (!string.IsNullOrEmpty(LastSelectedCadUnit) && cbbCadUnit.Items.Contains(LastSelectedCadUnit))
     {
         cbbCadUnit.SelectedItem = LastSelectedCadUnit;
     }
     else
     {
         cbbCadUnit.SelectedIndex = 0;
     }
     cbCalibrateBasepoint.IsChecked = LastCalibrateBasepoint;
     ```
3. Trong sự kiện `btOk_Click`:
   - Cập nhật lại biến static:
     ```csharp
     LastSelectedCadUnit = SelectedCadUnit;
     LastCalibrateBasepoint = IsCalibrateBasepoint;
     ```

---

### B. Nâng cấp `ConnectSprinklerFlexPipeCmd.cs` (Cả `src/net8.0-windows/` và `src/net48/`)

#### 1. Mở rộng bộ lọc chọn đối tượng (Selection Filter)
- Trong `IsPhase2SelectableElement(Element element)`:
  ```csharp
  private static bool IsPipeFitting(FamilyInstance fi)
  {
      return fi?.Category != null && fi.Category.GetIdInt() == (int)BuiltInCategory.OST_PipeFitting;
  }

  private static bool IsPhase2SelectableElement(Element element)
  {
      return element is Pipe || (element is FamilyInstance fi && (IsSprinkler(fi) || IsPipeFitting(fi)));
  }
  ```
- Cập nhật `SprinklerAndPipeSelectionFilter`: Cho phép chọn cả `Pipe`, `FamilyInstance` là Sprinkler hoặc PipeFitting.
- Cập nhật thông báo chọn (Prompt): *"Chọn Pipe, PipeFitting hoặc Sprinkler tiếp theo (Esc để kết thúc)"*.

#### 2. Tự động nhận diện Fitting gắn ở đầu ống (Existing Attached Fitting)
Trong `ExecuteConnectFlexPipeDirect`:
- Khi kiểm tra `pickedPipe`:
  ```csharp
  List<Connector> unusedConns = GetUnusedConnectors(pickedPipe);
  ```
  Nếu `unusedConns.Count == 0`:
  - Quét các connector đầu nối (ConnectorType.End) của `pickedPipe`.
  - Với mỗi connector `c`:
    Nếu `c.IsConnected`: duyệt `c.AllRefs`:
    Tìm phần tử nối vào `c`: nếu là một `FamilyInstance fitting` thuộc `OST_PipeFitting`:
    Tìm connector hở (unused piping connector) trên fitting đó:
    ```csharp
    Connector openFittingConn = GetUnusedConnectors(fitting)
        .FirstOrDefault(fc => fc.Domain == Domain.DomainPiping);
    ```
    Nếu tìm thấy `openFittingConn`:
    -> Thêm `openFittingConn` vào danh sách đầu chờ `unusedConns`.
    -> Ghi nhận cờ `isAttachedFitting = true` và lưu tham chiếu `attachedFitting = fitting`.
- Khi `unusedConns.Count > 0` (dù là đầu hở trực tiếp của ống hay đầu hở từ Fitting gắn sẵn ở đầu ống):
  -> Tool xác định đây là chế độ **Ống nhánh (Branch Mode)**:
  `bool isExistingBranchPipe = true;`
  -> Tuyệt đối **KHÔNG** nhảy vào nhánh Main-pipe/Tee mode và không kiểm tra `hasTeeClearance`.
- Khi tạo kết nối FlexPipe cho đầu hở của Fitting:
  - Đầu `topTarget` chính là `openFittingConn`.
  - Không cần tạo thêm cút phụ (Elbow) nếu đầu hở của fitting đã hướng xuống hoặc sẵn sàng nối ống mềm.
  - Kết nối FlexPipe trực tiếp từ `openFittingConn` xuống Sprinkler (thông qua reducer nếu khác đường kính).

#### 3. Xử lý khi người dùng click chọn trực tiếp vào `PipeFitting`
- Trong luồng chọn tương tác (`Execute` và `ExecuteMultiElbow`):
  - Nếu phần tử thứ nhất hoặc thứ hai là `FamilyInstance fitting` (loại `OST_PipeFitting`):
  - Tìm connector hở `openConn` trên fitting đó.
  - Nếu có `openConn`:
    Lấy điểm xuất phát là `openConn.Origin`.
    Thực hiện nối FlexPipe trực tiếp từ `openConn` tới connector của Sprinkler.

---

## 3. Tiêu chí Nghiệm thu (Acceptance Criteria)
1. Cả 2 target `net48` và `net8.0-windows` biên dịch thành công 0 Error, 0 Warning khi chạy:
   `powershell -ExecutionPolicy Bypass -File .\Dev-BIN.ps1`
2. Mở `PlaceFamilyWindow`: Khi chọn một đơn vị (ví dụ `Inches (in)`) và bấm OK, lần chạy lệnh tiếp theo hộp thoại tự động ghi nhớ và chọn sẵn `Inches (in)`.
3. Trong trường hợp thực tế như ảnh lỗi người dùng gửi (ống nhánh đỏ đã có sẵn cút/Tê rẽ xuống ở đầu ống):
   Tool tự động nhận diện đầu chờ của Fitting, nối ống mềm FlexPipe từ Fitting xuống Sprinkler thành công mỹ mãn, **không còn báo lỗi `< 100mm`** và **không cần xóa fitting**.
