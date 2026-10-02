# TÀI LIỆU Ý TƯỞNG & LỘ TRÌNH PHÁT TRIỂN: MEP INSULATION PRO
**Dự án:** BIN TOOL - Bộ công cụ Revit MEP tự động hóa  
**Chủ đề:** Hệ thống bọc bảo ôn tự động đa hệ (Ống nước & Ống gió), Quản lý Presets và Xử lý cao độ trần/tầng thực tế  
**Ngày khởi tạo:** 02/10/2026  
**Trạng thái:** Sẵn sàng cho các pha phát triển tiếp theo  

---

## 1. Bối cảnh & Mục tiêu Kỹ thuật

### 1.1. Thực trạng trong mô hình hóa Revit MEP
- **Thực tế mô hình hóa:** Kỹ sư thường xuyên vẽ tuyến ống/ống gió trong không gian trần của một tầng (ví dụ: Trần tầng 18), nhưng lại chọn `Reference Level = Tầng 19` và nhập cao độ âm (ví dụ: `-500mm`).
- **Hạn chế của công cụ mặc định Revit:**
  - Gán bảo ôn thủ công từng đoạn ống rất tốn thời gian.
  - Phụ kiện (Fitting: cút, tê, côn) dễ bị bỏ sót, gây hở cổ ống trên bản vẽ 3D và thiếu khối lượng BOQ.
  - Bộ lọc thông thường dựa vào tham số `Reference Level` sẽ bỏ sót hoặc phân loại sai các phần tử trần.
- **Mục tiêu của MEP Insulation Pro:**
  - Tự động hóa 100% việc bọc bảo ôn cho cả **Ống Nước (Pipe & Fitting)** và **Ống Gió (Duct & Fitting)**.
  - Quản lý quy tắc độ dày theo từng Hệ thống chuyên biệt thông qua **Presets Tiêu chuẩn** (Chiller, Nước ngưng, Nước nóng, Gió cấp, Gió hồi, Hút khói chống cháy EI).
  - Tích hợp thuật toán lọc theo **Cao độ không gian thực tế ($Z$-Spatial Bounding)** thay vì chỉ dựa vào tham số phẳng.

---

## 2. Kiến trúc Chức năng Đề xuất

```
+-----------------------------------------------------------------------------------+
|                           MEP INSULATION PRO (WINDOW)                            |
+-----------------------------------------------------------------------------------+
|  [ TAB 1: ỐNG NƯỚC (PIPE) ]               |  [ TAB 2: ỐNG GIÓ (DUCT) ]            |
+-------------------------------------------+---------------------------------------+
|  1. LOẠI BẢO ÔN (Insulation Type):                                                |
|     - Quét từ Revit Document: OST_PipeInsulations / OST_DuctInsulations           |
|     - Checkbox: [X] Tự động xóa lớp bảo ôn cũ trước khi bọc lại                   |
+-----------------------------------------------------------------------------------+
|  2. PHẠM VI ÁP DỤNG (SCOPE):                                                      |
|     (o) Theo System Type (Khuyên dùng): Dropdown danh sách hệ + Số lượng phần tử  |
|     (o) Đường ống đang chọn: Chọn trước hoặc quét chọn trực quan                  |
|     (o) Tất cả phần tử trong View: Quét View Range hiển thị                       |
|     (o) MỚI - Theo Tầng thực tế (Spatial Level): Quét theo dải cao độ Z hình học   |
+-----------------------------------------------------------------------------------+
|  3. QUẢN LÝ PRESETS ĐỘ DÀY (Thickness Rules Engine):                              |
|     - Preset Dropdown: Chiller C1 | Nước Ngưng C1 | Gió Cấp | Hút Khói EI | Custom |
|     - DataGrid 4 cột: [Hệ Thống] | [Từ Cỡ (mm)] | [Đến Cỡ (mm)] | [Độ Dày (mm)]   |
|     - Nút điều khiển: [💾 Lưu Preset] | [🔄 Nạp chuẩn C1] | [➕ Thêm] | [➖ Xóa]    |
+-----------------------------------------------------------------------------------+
|  [ Đóng (ESC) ]                                    [ THỰC HIỆN BỌC BẢO ÔN (ENTER) ]|
+-----------------------------------------------------------------------------------+
```

---

## 3. Chi tiết Giải Pháp Kỹ Thuật Đột Phá

### 3.1. Giải pháp lọc phần tử trên trần tầng (Ceiling vs Reference Level)

#### Vấn đề:
Ống nằm ở trần tầng 18 nhưng mang tham số `Reference Level = Tầng 19` (offset âm).

#### 3 Cơ chế xử lý chuẩn xác:
1. **Cơ chế 1: `FilteredElementCollector(doc, doc.ActiveView.Id)` (Hiện tại)**
   - Revit API lọc dựa trên **View Range (Cắt & Hiển thị đồ họa)** của View hiện hành.
   - Bất kể phần tử gán Reference Level nào, miễn là hình học 3D của nó nằm trong phạm vi từ `Bottom/View Depth` đến `Top` của View 18, nó **được gom 100%**.
2. **Cơ chế 2: Quét qua View 3D Section Box**
   - Người dùng cô lập tầng 18 bằng Section Box trong View 3D.
   - Công cụ gom toàn bộ phần tử nằm trọn trong Section Box, không phụ thuộc vào View Range 2D.
3. **Cơ chế 3: Lọc theo Cao độ Hình học Không gian (Spatial Elevation Filtering - Đề xuất phát triển)**
   - Lấy cao độ sàn tầng hiện tại $Z_{floor}$ và sàn tầng trên $Z_{ceiling}$.
   - Với mỗi ống/ống gió, lấy tọa độ tim $Z_{center} = \frac{p_{start}.Z + p_{end}.Z}{2}$.
   - Điều kiện gom phần tử:
     $$Z_{floor} - \epsilon \le Z_{center} \le Z_{ceiling} + \delta$$
   - **Ưu điểm tuyệt đối:** Chấp mọi trường hợp người dựng mô hình gán sai Level, vẽ từ tầng khác phóng sang, hoặc thiết lập View Range 2D bị che khuất!

---

### 3.2. Mở rộng tính năng Bọc Bảo Ôn Ống Gió (Duct Insulation)

#### 1. Các hệ thống ống gió mục tiêu:
- **Gió Cấp (Supply Air):** Cách nhiệt ngăn đọng sương, độ dày phổ biến 25mm / 32mm / 40mm (Bông thủy tinh dán bạc hoặc Cao su lưu hóa).
- **Gió Hồi (Return Air):** Cách nhiệt 25mm khi đi qua các không gian không điều hòa.
- **Hút Khói Sự Cố (Smoke Exhaust):** Bọc bông khoáng chống cháy (Rockwool) đạt giới hạn chịu lửa EI 30, EI 60, EI 120 theo QCVN 06:2022 (độ dày thường 50mm - 100mm).
- **Gió Tươi (Fresh Air / Outdoor Air):** Cách nhiệt ngăn đọng sương vào mùa hè hoặc ngăn thất thoát nhiệt.

#### 2. Tiêu chí tính toán độ dày ống gió:
Khác với ống nước dựa vào đường kính danh định (DN), ống gió chữ nhật được tính dựa trên:
- **Kích thước cạnh lớn nhất:** $\max(Width, Height)$
  - Cạnh $\le 800$ mm: Độ dày 25 mm.
  - $800 <$ Cạnh $\le 2000$ mm: Độ dày 32 mm hoặc 40 mm.
  - Cạnh $> 2000$ mm: Độ dày 50 mm kết hợp khung xương gia cường.
- **Ống gió tròn (Round Duct):** Dựa vào đường kính $D$.

#### 3. Revit API áp dụng:
```csharp
// Kiểm tra loại vật liệu bảo ôn ống gió
ElementId ductInsulationTypeId = ...; // Category OST_DuctInsulations

// Bọc ống gió thẳng
DuctInsulation.Create(doc, duct.Id, ductInsulationTypeId, thicknessFeet);

// Bọc phụ kiện ống gió (Co cút, Tê, Côn thu)
DuctInsulation.Create(doc, ductFitting.Id, ductInsulationTypeId, thicknessFeet);
```

---

### 3.3. Xử lý đồng bộ Phụ Kiện (Fitting Connector Engine)

Để tránh hiện tượng cút, tê, côn bị bỏ sót hoặc gán sai kích thước:
1. **Truy xuất Connector Manager của Fitting:**
   ```csharp
   FamilyInstance fi = fitting as FamilyInstance;
   ConnectorManager cm = fi?.MEPModel?.ConnectorManager;
   ```
2. **Tìm kích thước danh định lớn nhất:**
   - Với cổng tròn: $\max(Radius \times 2)$.
   - Với cổng chữ nhật: $\max(Width, Height)$.
3. **Tra cứu bảng quy tắc của Hệ tương ứng:**
   - Lấy độ dày bảo ôn tiêu chuẩn của kích thước lớn nhất để bọc trùm lên toàn bộ fitting.
   - Đảm bảo độ kín khít và đồng bộ bề mặt khi quan sát trên phối cảnh 3D.

---

## 4. Cấu trúc Lưu trữ Presets (`presets.json`)

Tệp cấu hình lưu trữ độc lập tại `%APPDATA%\BIN_PipeInsulation\presets.json` (hoặc mở rộng thành `mep_insulation_presets.json`):

```json
{
  "schemaVersion": 2,
  "lastUsedCategory": "Pipe",
  "pipePresets": {
    "Chiller_C1": [
      { "system": "CHWS/CHWR", "minDN": 20, "maxDN": 40, "thicknessMM": 32 },
      { "system": "CHWS/CHWR", "minDN": 50, "maxDN": 150, "thicknessMM": 40 },
      { "system": "CHWS/CHWR", "minDN": 200, "maxDN": 600, "thicknessMM": 50 }
    ],
    "Condensate_C1": [
      { "system": "CDP/CONDENSATE", "minDN": 20, "maxDN": 65, "thicknessMM": 19 },
      { "system": "CDP/CONDENSATE", "minDN": 80, "maxDN": 600, "thicknessMM": 25 }
    ],
    "HotWater": [
      { "system": "DHW/HOT", "minDN": 15, "maxDN": 32, "thicknessMM": 25 },
      { "system": "DHW/HOT", "minDN": 40, "maxDN": 65, "thicknessMM": 32 },
      { "system": "DHW/HOT", "minDN": 80, "maxDN": 300, "thicknessMM": 40 }
    ]
  },
  "ductPresets": {
    "SupplyAir": [
      { "system": "SUPPLY AIR", "minSize": 100, "maxSize": 800, "thicknessMM": 25 },
      { "system": "SUPPLY AIR", "minSize": 850, "maxSize": 2500, "thicknessMM": 32 }
    ],
    "ReturnAir": [
      { "system": "RETURN AIR", "minSize": 100, "maxSize": 2500, "thicknessMM": 25 }
    ],
    "SmokeExhaust_EI": [
      { "system": "SMOKE EXHAUST", "minSize": 100, "maxSize": 3000, "thicknessMM": 50 }
    ]
  }
}
```

---

## 5. Lộ trình Triển khai (Next Steps Roadmap)

| Giai đoạn | Nội dung công việc | Kết quả đầu ra |
| :--- | :--- | :--- |
| **Pha 1 (Đã hoàn thành)** | Nâng cấp Pipe Insulation: 5 Presets, Dropdown hệ thống kèm số lượng ống, Pure WPF UI, Hot-Reload. | Bản build `20261002-0950` ổn định trên cả .NET 4.8 & .NET 8. |
| **Pha 2** | Triển khai Tab **Ống Gió (Duct Insulation)** vào giao diện: Quét `OST_DuctCurves` & `OST_DuctFitting`, tính độ dày theo cạnh lớn nhất. | Hợp nhất công cụ thành **MEP Insulation Pro**. |
| **Pha 3** | Bổ sung tùy chọn phạm vi **Spatial Level ($Z$-Elevation)**: Tự động tính toán không gian trần giữa 2 sàn tầng để không bao giờ bị sót ống. | Loại bỏ 100% rủi ro gán sai Reference Level trong dự án lớn. |
| **Pha 4** | Kiểm tra va chạm sơ bộ sau khi bọc bảo ôn (Pre-Clash Check) cảnh báo các vị trí ống chạm dầm hoặc chạm ống khác sau khi độ dày tăng lên. | Báo cáo danh sách xung đột tiềm ẩn cho kỹ sư xử lý. |

---
*Tài liệu được lưu trữ trực tiếp trong repository để sẵn sàng kế thừa và kích hoạt phát triển bất kỳ lúc nào.*
