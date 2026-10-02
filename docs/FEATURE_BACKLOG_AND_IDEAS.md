# SỔ TAY Ý TƯỞNG & LỘ TRÌNH TÍNH NĂNG MỚI (FEATURE BACKLOG & IDEAS)
**Dự án:** BIN TOOL - Bộ công cụ Revit MEP tự động hóa toàn diện  
**Đơn vị quản lý:** BIM Team & Antigravity Lead Coordinator  
**Cập nhật lần cuối:** 02/10/2026  

---

## 1. Mục Đích & Quy Trình Quản Lý Ý Tưởng

Tài liệu này là **Trung tâm tích lũy ý tưởng (Master Backlog)** của toàn bộ hệ thống BIN TOOL (75+ công cụ trên 2 Tab Ribbon).
- Bất cứ khi nào bạn có ý tưởng mới, yêu cầu cải tiến UI/UX, hoặc gặp các bài toán thực tế ngoài công trường/dự án mà chưa muốn code ngay $\rightarrow$ **Lập tức ghi lại vào đây**.
- Mỗi ý tưởng sẽ được phân tích sẵn giải pháp kỹ thuật, đánh giá mức độ ưu tiên và các hàm Revit API cần dùng, sẵn sàng kích hoạt bất kỳ lúc nào bạn yêu cầu triển khai.

### Quy ước Trạng thái Ý tưởng:
- 💡 **[PROPOSED]** - Ý tưởng mới đề xuất, đang ghi nhận yêu cầu sơ bộ.
- 📐 **[SPECIFIED]** - Đã có giải pháp kỹ thuật, UI/UX mockup và lộ trình rõ ràng, sẵn sàng code.
- 🔨 **[IN PROGRESS]** - Đang được triển khai mã nguồn bởi Codex & Antigravity.
- ✅ **[RELEASED]** - Đã hoàn thành, kiểm định chất lượng và đóng gói phát hành.

---

## 2. Bảng Tổng Hợp Ý Tưởng Đang Chờ Triển Khai

| ID | Nhóm Công Cụ | Tên Ý Tưởng / Tính Năng Đề Xuất | Độ Ưu Tiên | Trạng Thái |
| :---: | :--- | :--- | :---: | :---: |
| **IDEA-01** | BIM - MEP | **Bọc bảo ôn Ống Gió (Duct Insulation)** & Bọc chống cháy EI | Cao | 📐 SPECIFIED |
| **IDEA-02** | BIM - MEP | **Lọc cao độ trần thực tế (Spatial $Z$-Level Filtering)** cho Pipe/Duct | Cao | 📐 SPECIFIED |
| **IDEA-03** | BIM - MEP | **Avoid Clash 3.0: Né dầm cho chùm nhiều ống song song (Multi-Pipe)** | Trung bình | 💡 PROPOSED |
| **IDEA-04** | BIM - DRAINAGE | **Kiểm tra độ dốc tự động (Slope Health Check)** & Báo ống chảy ngược | Cao | 💡 PROPOSED |
| **IDEA-05** | BIM - SPRINKLER| **Auto-Grid Sprinkler theo Room**: Rải đầu phun tự động theo TCVN 7336 / NFPA | Cao | 💡 PROPOSED |
| **IDEA-06** | BIM - MEP | **CAD-to-BIM Learning**: Tự động học vị trí Miệng gió, FCU, Bơm từ CAD sang Revit | Rất Cao | 📐 SPECIFIED |
| **IDEA-07** | BIM - SUPPORT | **Auto Hanger/Support Spacing**: Tự rải giá treo theo khoảng cách tiêu chuẩn | Trung bình | 💡 PROPOSED |
| **IDEA-08** | BIM - CHECK | **Clash & Clearance Matrix**: Quét nhanh khoảng cách an toàn ống với dầm/cột | Trung bình | 💡 PROPOSED |
| **IDEA-09** | BIM - MEP | **Khử khối vuông ở Tê (Smooth Tee Insulation)**: Tự động làm mượt ngã ba chữ T | Rất Cao | 📐 SPECIFIED |

---

## 3. Chi Tiết Từng Ý Tưởng Phát Triển

---

### 💡 IDEA-01: Bọc Bảo Ôn Ống Gió (Duct Insulation Pro) & Chống Cháy EI
- **Nhóm công cụ:** `BIM - MEP` (Tích hợp vào `Pipe Insulation` thành `MEP Insulation Pro` hoặc nút riêng).
- **Mục tiêu:**
  - Tự động bọc bảo ôn cho **Ống gió thẳng (Duct)** và **Phụ kiện ống gió (Duct Fitting)**.
  - Phân loại theo hệ thống:
    - 🌪️ **Gió Cấp (Supply Air)**: Cách nhiệt ngăn đọng sương, độ dày phổ biến 25mm / 32mm / 40mm (Bông thủy tinh cuộn bạc hoặc Cao su lưu hóa).
    - 💨 **Gió Hồi (Return Air)**: Cách nhiệt 25mm.
    - 🌿 **Gió Tươi (Fresh Air)**: Cách nhiệt 25mm.
    - 🚫 **Hút Khói Sự Cố (Smoke Exhaust)**: Bọc bông khoáng chống cháy (Rockwool) đạt giới hạn chịu lửa **EI 30, EI 60, EI 120** theo QCVN 06:2022 (độ dày 50mm - 100mm).
- **Cơ chế tính toán:**
  - Dựa trên kích thước cạnh lớn nhất của ống gió chữ nhật: $\max(Width, Height)$ hoặc đường kính ống tròn $(D)$.
  - Phụ kiện ống gió: Tự động bắt theo kích thước cổng kết nối lớn nhất và bọc kín không bị hở góc.
- **Revit API sử dụng:**
  - `DuctInsulation.Create(doc, duct.Id, ductInsulationTypeId, thicknessFeet)`
  - `DuctInsulation.Create(doc, ductFitting.Id, ductInsulationTypeId, thicknessFeet)`

---

### 💡 IDEA-02: Lọc Cao Độ Không Gian Thực Tế (Spatial $Z$-Level Filtering)
- **Nhóm công cụ:** Dùng chung cho `Pipe Insulation`, `Avoid Clash`, `MEP Offset`, `Select By Parameter`.
- **Thực tế kỹ thuật:** Kỹ sư vẽ ống trong không gian trần tầng 18 nhưng chọn `Reference Level = Tầng 19` (offset âm `-500mm`). Nếu lọc theo tham số `Reference Level` sẽ bị sót phần tử.
- **Giải pháp:**
  - Bổ sung tùy chọn phạm vi: `Theo Tầng Thực Tế (Spatial Level Height)`.
  - Thuật toán: Lấy cao độ sàn tầng hiện tại $Z_{floor}$ và sàn tầng trên $Z_{ceiling}$.
  - Điều kiện gom phần tử tự động:
    $$Z_{floor} - \epsilon \le Z_{\text{ống}} \le Z_{ceiling} + \delta$$
  - **Lợi ích:** Gom chính xác 100% các phần tử thuộc tầng đó mà **không phụ thuộc vào việc người vẽ gán sai Reference Level** hay thiết lập View Range 2D bị che khuất!

---

### 💡 IDEA-03: Avoid Clash 3.0 - Né Dầm Cho Chùm Nhiều Ống Song Song (Multi-Pipe Bypass)
- **Nhóm công cụ:** `BIM - MEP` (`AvoidClashCmd`).
- **Mục tiêu:**
  - Hiện tại Avoid Clash uốn né từng ống một.
  - Ý tưởng mới: Cho phép quét chọn **chùm 3-5 ống chạy song song** cùng vượt qua một dầm kết cấu lớn.
  - Tool tự động tính toán uốn né đồng bộ cả chùm ống:
    - Giữ nguyên khoảng cách tim ống (Center-to-Center spacing) trước và sau khi uốn né.
    - Tự động so le các co cút nếu kích cỡ ống khác nhau để không bị va chạm giữa các co lân cận.
    - Giữ trật tự hệ thống tuyến ống đẹp mắt như thi công thực tế.

---

### 💡 IDEA-04: Drainage Smart Slope & Flow Health Check
- **Nhóm công cụ:** `BIM - DRAINAGE`.
- **Mục tiêu:**
  - Hệ thống thoát nước yêu cầu độ dốc khắt khe (1%, 1.5%, 2%).
  - Ý tưởng:
    - Tool quét toàn bộ tuyến ống thoát nước trong View.
    - Kiểm tra hướng dòng chảy: Cảnh báo ngay lập tức các đoạn ống bị **dốc ngược (Back-slope)** hoặc **võng (Sagging)** bằng màu đỏ.
    - Tự động điều chỉnh lại cao độ các Fitting để toàn tuyến đạt đúng độ dốc mong muốn chỉ với 1 click.

---

### 💡 IDEA-05: Auto-Grid Sprinkler Theo Room (Rải Đầu Phun Tự Động)
- **Nhóm công cụ:** `BIM - SPRINKLER`.
- **Mục tiêu:**
  - Hiện nay kỹ sư phải rải từng đầu phun Sprinkler bằng tay rất lâu.
  - Ý tưởng:
    - Người dùng chỉ cần click chọn 1 Phòng (`Room`) hoặc khu vực trần.
    - Tool tự động nhận diện diện tích, cấp nguy cơ cháy (Light Hazard / Ordinary Hazard) theo **TCVN 7336 / NFPA 13**.
    - Tự động tính toán số lượng đầu phun và khoảng cách lưới tối ưu (ví dụ: lưới $3.4m \times 3.4m$ hoặc $3.6m \times 3.6m$).
    - Tự động đặt các Family đầu phun gắn trần hoặc quay xuống và căn giữa các tấm trần thả (Ceiling Grid Align).

---

### 💡 IDEA-06: CAD-to-BIM Telemetry Learning (Đặt Thiết Bị Thông Minh)
- **Nhóm công cụ:** `BIM - MEP` (`PlaceFamilyCmd`).
- **Mục tiêu:**
  - Đã có khung kiến trúc Telemetry lưu trữ dữ liệu rải thiết bị.
  - Ý tưởng tiếp theo:
    - Khi kỹ sư click vào 1 block CAD (ví dụ block Miệng gió Diffuser hoặc block FCU), tool tự động nhận diện Block Name, Layer, và góc xoay.
    - Tự động gắn Family Revit tương ứng vào đúng vị trí và góc xoay của toàn bộ các block tương tự trên mặt bằng chỉ trong 1 giây.

---

### 💡 IDEA-07: Auto Support/Hanger Spacing (Tự Động Rải Giá Treo Đạt Chuẩn)
- **Nhóm công cụ:** `BIM - SUPPORT`.
- **Mục tiêu:**
  - Tự động tra cứu đường kính ống theo tiêu chuẩn khoảng cách giá treo (MSS SP-58 / SMACNA):
    - Ống $\le$ DN25: Khoảng cách giá treo 2.0m - 2.5m.
    - Ống DN32 - DN50: Khoảng cách 2.5m - 3.0m.
    - Ống $\ge$ DN65: Khoảng cách 3.5m - 4.0m.
  - Tự động rải Family Support bám vào đáy sàn bê tông bên trên, tự điều chỉnh chiều dài ty treo theo cao độ trần thực tế.

---

### 💡 IDEA-09: Khử Khối Vuông Ở Tê (Smooth Tee Insulation - Chế Độ Làm Mượt Ngã Ba Chữ T)
- **Nhóm công cụ:** `BIM - MEP` (`PipeInsulationCmd`, `PipeInsulationWindow`).
- **Vấn đề thực tế:**
  - Khi bọc bảo ôn lên phụ kiện chữ T (`PipeFitting - Tee`), thuật toán đồ họa nội tại của Revit không giải được giao cắt 3 mặt cong nên tự động bọc một **khối hộp chữ nhật/lăng trụ vuông thô kệch** bao quanh Tê (nhìn trên 3D rất xấu, bị gù và không thực tế).
- **Giải pháp kỹ thuật (Smooth Tee Option):**
  - Bổ sung tùy chọn thông minh trên giao diện:
    `[X] Khử khối vuông ở Tê (Smooth Tee Joints - Tự động làm mượt ngã ba chữ T)`
  - Khi bật tùy chọn này:
    - Tool vẫn bọc bảo ôn tròn cho toàn bộ **Ống chính (Header Pipe)**, **Ống nhánh (Branch Pipe)** và **Co cút (Elbow)**.
    - Riêng phụ kiện **Tee (chữ T)**: Tool chủ động **bỏ qua không bọc khối hộp của Fitting Tê**.
    - **Hiệu ứng đồ họa 3D:** Hai hình trụ bảo ôn tròn của Ống chính và Ống nhánh sẽ tự động đâm sát vào nhau tại ngã ba, tạo thành mối nối chữ T tròn trịa, trơn láng, tự nhiên như thợ thi công cắt mòi chữ V ngoài công trường, **biến mất hoàn toàn khối hộp vuông xấu xí**!
  - Vẫn tính toán và xuất khối lượng BOQ đầy đủ dựa trên diện tích bề mặt ống.
- **Trạng thái:** 📐 SPECIFIED (Đã có phương án, sẵn sàng tích hợp).

---

## 4. Mẫu Ghi Nhận Ý Tưởng Nhanh (Template Cho Tương Lai)

Khi bạn có ý tưởng mới bất kỳ, chỉ cần gửi yêu cầu vắn tắt, Antigravity sẽ tự động cập nhật vào tài liệu này theo mẫu sau:

```markdown
### 💡 IDEA-[Số Thứ Tự]: [Tên Ý Tưởng]
- **Nhóm công cụ:** [BIM - MICRO / MEP / SPRINKLER / DRAINAGE / SUPPORT / CHECK / DOCS]
- **Vấn đề thực tế:** [Mô tả khó khăn hoặc thao tác thủ công tốn thời gian hiện tại]
- **Giải pháp đề xuất:** [Mô tả tính năng hoặc giao diện mong muốn]
- **Revit API & Thuật toán:** [Các hàm DB / Selection / Geometry cần dùng]
- **Độ ưu tiên:** [Thấp / Trung bình / Cao / Khẩn cấp]
- **Trạng thái:** 💡 PROPOSED
```

---
*Tài liệu này được lưu trữ trực tiếp trong thư mục `docs/` của mã nguồn và được đồng bộ Git thường xuyên.*
