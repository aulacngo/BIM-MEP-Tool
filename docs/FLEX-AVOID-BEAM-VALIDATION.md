# Flex Duct / Flex Pipe tránh dầm có bảo ôn

Ngày kiểm tra source/build: 2026-10-03. Phạm vi: lệnh `FlexDuctAvoidMepCmd`, cửa sổ `AvoidMepWindow`, hai selection filter và hai helper `FlexAvoidGeometry` / `FlexAvoidMath`. Sáu cặp C# giữa `net48` và `net8.0-windows` được kiểm tra byte-for-byte. Không hot-load DLL hoặc thao tác mô hình Revit trong lần kiểm tra này.

## Hành vi đã triển khai

1. Chọn Flex Duct hoặc Flex Pipe trong Host, sau đó chọn một cấu kiện 3D trong Host hoặc Revit Link. Chọn Link bằng `PointOnElement`; filter kiểm tra `LinkedElementId` trong document của Link. Flex trong Link không được chỉnh sửa từ Host.
2. Đọc đường kính danh nghĩa; tự nhận diện bảo ôn từ instance/type và insulation gắn với đối tượng nếu API hỗ trợ. Bán kính dùng kiểm tra là `Ø/2 + insulation + clearance`. Clearance mặc định 50 mm, hướng mặc định xuống dưới đáy dầm.
3. CHECK chỉ đọc hình học, chọn/highlight hai đối tượng và zoom tới vùng tâm spline gần vật cản nhất. Báo khoảng cách có dấu, khoảng hở vỏ ngoài, độ ăn sâu, phần thiếu Clearance, cận dưới bảo thủ, sai số và tọa độ XYZ. Không tạo transaction hay marker trong mô hình.
4. AUTO-AVOID bố trí control points đi dưới mặt thấp nhất hoặc trên mặt cao nhất trong hành lang tuyến. Giữ nguyên hai đầu và tangents; kiểm tra vị trí, hướng và danh tính kết nối của cả hai connector. Sàn/dầm dài phủ qua hai đầu cũng được hỗ trợ khi hai đầu đã nằm ở phía an toàn theo hướng chọn.
5. Mỗi tuyến thử nằm trong `SubTransaction` và được rollback trước khi thử tuyến tiếp theo. Transaction ghi tên **Flex Duct Avoid Beam**. Sau commit, kiểm tra lại spline/kết nối và không gian lân cận trước khi assimilate `TransactionGroup`; lỗi sẽ rollback nhóm.
6. Giao diện Pure C# WPF có card, badge, Canvas vector mặt đứng dầm I, bảo ôn và khoảng hở. Có hai nút CHECK/AUTO-AVOID, ENTER chạy Auto-Avoid, ESC đóng, nút khôi phục bảo ôn tự nhận diện. Khi đổi thông số, kết quả cũ được đánh dấu cần kiểm tra lại. Ghi đè bảo ôn trên UI chỉ đổi giả định tính toán, không đổi insulation trong mô hình.

## Cơ sở hình học và giới hạn

- Solid được lấy ở mức Fine, duyệt `GeometryInstance.GetInstanceGeometry()`. Geometry family đã có transform của instance; `link.GetTotalTransform()` chỉ áp dụng một lần để đưa Solid vào Host.
- Khoảng cách tới mặt Solid dùng `Face.Project`; trường hợp projection ngoài mặt trimmed được bổ sung bằng projection tới edge và hai đầu edge. Tâm nằm trong thể tích được xác định bằng `Solid.IntersectWithCurve` và cho khoảng cách âm. Với nhiều Solid, dùng minimum các khoảng cách có dấu; ở vùng các Solid chồng lấn, đây là đại lượng bảo thủ cho kiểm tra khoảng hở, không phải phép đo sâu bên trong một Boolean union đã hợp nhất.
- Đường tâm lấy từ `LocationCurve` hoặc một Curve duy nhất có hai đầu khớp với Flex trong geometry Revit. Không dựng một spline giả từ control points để tuyên bố tuyến thực đã đạt.
- Các khoảng tham số Hermite được đổi thành cubic Bézier bằng giá trị và đạo hàm đầu/cuối. Kiểm tra thêm tại 1/4, 1/2 và 3/4 mỗi khoảng để từ chối representation không phù hợp. Line thực do Revit trả về cũng được hỗ trợ.
- Với mỗi cubic, tâm mẫu và convex hull của control points tạo một cận dưới khoảng cách nhờ tính chất 1-Lipschitz. Chia nhỏ đoạn có cận dưới nhỏ nhất đến khi khoảng giữa upper/lower bound không quá 0,5 mm. Điều kiện chấp nhận là **cận dưới ≥ R_total**, tránh bỏ sót cánh I mỏng giữa các điểm mẫu. Đây là tính toán có dung sai, không phải tuyên bố hình học số học tuyệt đối.
- Mesh các mặt Solid chỉ dùng đề xuất tuyến và cắt hành lang ngang cho dầm dài/chéo. BoundingBox chỉ dùng broad phase và tìm cấu kiện lân cận; chúng không quyết định kết quả clearance. Kết quả cuối dùng lại khoảng cách tới mặt/edge của Solid và spline thực.
- Mỗi phép đo giới hạn 8.192 lần đánh giá / 8 giây; xử lý tuyến kiểm tra ngân sách thời gian 30 giây trước mỗi bước. Ngân sách được kiểm tra giữa các API call và không ngắt một native API call đang chạy. Các giới hạn này không phải chứng minh P95/P99 hoặc không lag trong Revit.
- Auto-Avoid kiểm tra vùng tuyến mới với Solid lân cận trong Host và Link đã tải, tối đa 128 ứng viên / 32 đối tượng có Solid. Flex, lớp insulation của nó và các cấu kiện đang nối trực tiếp vào hai đầu được loại khỏi kiểm tra không gian vì mối nối chủ đích đã chia sẻ không gian. Không kiểm tra toàn bộ mô hình. Không gian trong Link lồng nhau/chưa tải, geometry không xác minh được, vùng quá phức tạp hoặc tuyến không đủ chỗ sẽ không được chấp nhận.
- Không tự thử một tuyến khác sau khi tuyến đạt vật cản chính lại bị cấu kiện lân cận chặn; thao tác rollback và báo cấu kiện đó để người dùng đổi hướng hoặc bố trí thủ công.
- RevitAPI 2023 trên máy không có enum `RBS_FLEX_DUCT_INSULATION_THICKNESS`. Code resolve tên enum này khi phiên bản cung cấp, đồng thời hỗ trợ `RBS_REFERENCE_INSULATION_THICKNESS`, `RBS_PIPE_INSULATION_THICKNESS`, các tham số insulation khác và `InsulationLiningBase.GetInsulationIds`. Không thấy lớp/giá trị thì UI báo rõ và cho nhập giá trị thực tế.

## Kết quả kiểm tra offline

```powershell
dotnet build src/net48/BIN.csproj -c Release --no-restore --nologo -v minimal
dotnet build src/net8.0-windows/BIN.csproj -c Release --no-restore --nologo -v minimal
dotnet run --project scripts/tests/FlexAvoid/FlexAvoid.Tests.csproj -c Release
```

| Kiểm tra | Kết quả |
| --- | --- |
| Release net48 | 0 warnings, 0 errors |
| Release net8.0-windows | 0 warnings, 0 errors |
| Harness hình học giải tích + WPF + artifact | PASS 95 assertions |
| Sáu cặp file C# | Byte-for-byte giống nhau |
| Resource `avoidmepwindow.baml` trong hai DLL | Không còn |
| BAML cũ / đăng ký resource trong hai project | Đã xóa |
| Render WPF hai hướng | Đã kiểm tra PNG, không cắt chữ/control |
| Revit native kernel / mô hình thật / transaction thật | Chưa thử |

Harness dùng vector primitive và signed-distance giải tích độc lập cho box, profile I và sphere, kiểm tra bảo ôn-only clash, tâm nằm trong web, vật cản mỏng giữa mẫu, đường cong clash dù endpoint chord không clash, translation, dầm chéo, tuyến dưới sàn, thiếu chỗ chuyển tiếp, input hữu hạn theo vi-VN/en-US, override, CHECK/AUTO dispatch, ENTER/ESC, stale result và callback lỗi. Không mock một Revit transaction rồi gọi đó là bằng chứng rollback thực tế.

Ảnh UI từ fixture, không phải kết quả mô hình: `scratch/flex-avoid-qa/down-check.png` và `up-check.png`.

## Cần kiểm tra trực tiếp trong Revit

| Trường hợp | Tiêu chí |
| --- | --- |
| Flex Duct có bảo ôn, dầm bê tông Host | CHECK phát hiện clash vỏ; Auto xuống giữ hai đầu, đạt cận dưới Clearance |
| Flex Pipe có bảo ôn, dầm I Host | Không bỏ sót flange/web; diameter/bảo ôn hiển thị đúng |
| Dầm I trong Link dịch chuyển và quay | Solid, vị trí highlight và tuyến cùng tọa độ Host |
| Sàn/dầm dài, hai đầu sẵn nằm dưới | Né xuống không đòi hai đầu nằm ngoài footprint ngang |
| Hướng lên có sàn/ống khác chặn | Báo vật cản lân cận và rollback toàn bộ tuyến |
| Hai đầu có kết nối / Flex bị Pinned / không đủ chỗ | Không mất liên kết; không để lại tuyến thử |
| Revit trả curve không hỗ trợ / geometry lỗi | Báo chưa xác minh, không thông báo đạt giả |
| Một cảnh báo/lỗi xuất hiện khi commit | Xác minh failure processing và group rollback bằng mô hình thực |
| Tắt/mở lại command, Undo | Transaction có đúng tên; hành vi Undo như dự kiến |
| DPI 125–200%, view 2D/3D | UI không mất nút; selection/zoom hoạt động hoặc có thông báo view rõ ràng |

## Tham khảo API chính thức

- [Autodesk: Face.Project](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/4bee3e30-74fa-3103-c2f4-d07618fbcedf.htm): projection ngoài trimmed face có thể trả null.
- [Autodesk: Lining and insulation](https://help.autodesk.com/cloudhelp/2024/PTB/Revit-API/files/Revit_API_Developers_Guide/Discipline_Specific_Functionality/MEP_Engineering/MEP_Element_Creation/Revit_API_Revit_API_Developers_Guide_Discipline_Specific_Functionality_MEP_Engineering_MEP_Element_Creation_Create_Pipes_and_Ducts_html.html): insulation gắn với host được truy xuất bằng `GetInsulationIds`.
- Kiểm tra thêm với `RevitAPI.xml` / `RevitAPIUI.xml` cài tại Revit 2023 trên máy cho Flex.Points, tangents, Hermite Parameters/ComputeDerivatives, IntersectWithCurve và SetReferences.
