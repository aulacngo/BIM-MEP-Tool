# Kết quả triển khai Telemetry & Diagnostics V2+

Ngày kiểm tra: 2026-10-03. Phạm vi: source local, build, harness C#, HTTP loopback và Worker với SQLite. Không triển khai lên Cloudflare/D1 và không thay DLL đang chạy trong Revit.

## Thay đổi đã hoàn tất

- Chuẩn hóa DocumentChanged sang envelope L1/schema 1.0; lưu operation, transaction state, counts, category sample và chất lượng dữ liệu.
- Một outbox dùng chung giữa các DLL hot-load, tối đa 200 item và 2 MiB dung lượng giữ lại được tính theo snapshot. Khi đầy bỏ item cũ nhất; nếu tranh chấp lock thì bỏ item mới ngay để không chặn UI.
- JSON, SHA256, GZip, ghi JSONL và HTTP chạy trong một consumer ThreadPool. Không còn hàng đợi ThreadPool riêng cho từng local log/payload factory. Batch tối đa 5 item, flush timer 5 giây; local log có rotation.
- Thêm scope invocation trong DevCommandProxy, correlation_id/parent_event_id xuyên L1/L2/document events và qua Assembly.Load(byte[]). Bổ sung exception type, callsite, stack có giới hạn và duration.
- Worker cô lập lỗi theo event, chuẩn hóa V1, chống ghi trùng, giới hạn giải nén và tham số SQL; lỗi phân tích anomaly không phủ nhận dữ liệu đã ghi.
- Thêm index correlation tương thích DB cũ, API truy vết và nút mở chi tiết trên dashboard. Giữ phần thay đổi dashboard có sẵn trước nhiệm vụ.
- Sửa thêm TelemetryHttpTransport, DevCommandProxy và MoveConnectCmd để hoàn tất giới hạn queue, correlation và chuyển xử lý JSON telemetry khỏi UI.

## Bằng chứng kiểm tra

| Kiểm tra | Kết quả |
| --- | --- |
| net48 Release, reference Revit 2023 | PASS, 0 warning / 0 error |
| net8.0-windows Release, reference Revit 2026 | PASS, 0 warning / 0 error |
| SHA256 6 cặp file C# | Giống nhau từng byte |
| Harness client net48 và net8 | 6 nhóm kiểm tra mỗi target, tất cả PASS |
| Revit API từ background trong harness | 0 vi phạm |
| HTTP thực qua loopback | 202, 503, gzip header, timeout đều PASS |
| Request bị giữ response | net48 khoảng 5.015 giây; net8 khoảng 3.021 giây |
| Worker + SQLite + fixture thực do C# phát sinh | 9 bài mỗi fixture target, tất cả PASS |
| Schema chạy lặp lại/index correlation/query plan | PASS |
| JavaScript Worker và script dashboard được render | PASS |
| git diff --check | PASS; chỉ có thông báo chuyển LF/CRLF |

Harness client kiểm tra contract, count/sample, zero-ID rollback, nguồn chưa biết, correlation, parent ID, nested scope, lỗi API metadata, exception, JSON hỏng, loại bỏ object không phải primitive, sanitizing MoveConnect, DLL nạp riêng, drop-oldest, giới hạn byte, tranh chấp lock, nhiều producer, sender đơn, mạng chậm, gzip và hồi phục sau lỗi serializer/transport.

Worker dùng SQL thực với SQLite in-memory. Các bài kiểm tra bao gồm batch V1/L1/L2 có hàng sai, receipt rejected/duplicate, cô lập lỗi persistence, analytics failure, 500 event, DB outage, decompressed payload limit, kích thước từng event, tham số SQL lớn nhiều ký tự escape, xác thực, truy vết và escape HTML. Đây không phải bằng chứng đã chạy trên hạ tầng D1 từ xa.

## Bộ nhớ và độ trễ: kết quả có giới hạn

Với 1.000 payload liên tiếp gần mức trần, queue dừng ở 128 item do chạm giới hạn byte:

| Harness | Heap giữ lại sau GC | Byte được outbox tính |
| --- | ---: | ---: |
| net48 | 2.076.696 | 2.081.792 |
| net8 | 2.091.504 | 2.081.792 |

Payload nhỏ giữ tối đa 200 item. Có kiểm tra thứ tự để xác nhận giữ những item mới nhất. Đây là heap giữ lại của workload kiểm tra; không phải cam kết process RSS dưới 3 MB hay không phát sinh allocation. Bộ đệm đang gửi nằm ngoài queue, bị giới hạn bởi batch 5 item và trần JSON từng item.

Đo 10.000 callback với API giả lập sau warm-up:

| Target | P50 (ms) | P95 (ms) | P99 (ms) | Max (ms) |
| --- | ---: | ---: | ---: | ---: |
| net48 | 0,0042 | 0,0053 | 0,0078 | 0,7336 |
| net8 | 0,0033 | 0,0060 | 0,0090 | 0,0770 |

**Chưa đạt bằng chứng bảo đảm mọi callback <0,2 ms.** Ngay harness net48 đã có outlier lớn hơn ngưỡng. Revit API, JIT, GC và scheduler không có hard deadline. Việc materialize stack trace chỉ xảy ra khi có exception và vẫn cần đọc trên UI để chỉ truyền primitive; đường lỗi phải được đo riêng. Không tuyên bố zero UI lag, zero allocation hoặc đã đo trên HCM-MEP63/model MEP thật.

## Ranh giới dữ liệu

- Scope BIN đang hoạt động là bằng chứng nguồn được lưu; ngoài scope chỉ có thể ghi `user_or_other_addin`, không tự xác nhận thao tác tay.
- Transaction terminal là trạng thái cuối quan sát từ DocumentChanged, không suy ra từ command result. Trường hợp không có notification vẫn là `unobserved`.
- Category là sample có cờ completeness; category của phần tử đã xóa không được suy đoán.
- L2 nhận flat anonymous primitive data có trần; dữ liệu bị cắt/không hỗ trợ có quality flag. JSON chi tiết lỗi không được phép phá batch.
- Outbox và local JSONL là best effort, không retry bền vững. Số `unacknowledged_delivery_count_since_start` không đồng nghĩa số event chắc chắn mất trên server.
- Envelope thiếu scope được ghi rõ unscoped. Các L1/L2 đã có correlation vẫn có thể thiếu một phía nếu event đó bị drop do overload.

## Tệp kiểm chứng và chạy lại

- `build-net48.log`, `build-net8.log`: build output.
- `client-net48.log`, `client-net8.log`: client tests, memory và latency.
- `transport-net48.log`, `transport-net8.log`: HTTP loopback.
- `worker-net48.log`, `worker-net8.log`: Worker/SQLite với fixture mỗi target.
- `parity.log`, `build-hashes.json`: source parity và SHA256 của DLL.
- `client-net48.json`, `client-net8.json`: envelope do source C# thực tạo ra với dữ liệu giả lập.

```powershell
dotnet build src/net48/BIN.csproj -c Release
dotnet build src/net8.0-windows/BIN.csproj -c Release
dotnet run --project scripts/tests/DocumentTelemetry/DocumentTelemetry.Tests.csproj -c Release -f net48 -- "$PWD/docs/tasks/TASK-20261003-TELEMETRY-V2/client-net48.json"
dotnet run --project scripts/tests/DocumentTelemetry/DocumentTelemetry.Tests.csproj -c Release -f net8.0 -- "$PWD/docs/tasks/TASK-20261003-TELEMETRY-V2/client-net8.json"
dotnet run --project scripts/tests/TelemetryTransport/TelemetryTransport.Tests.csproj -c Release -f net48
dotnet run --project scripts/tests/TelemetryTransport/TelemetryTransport.Tests.csproj -c Release -f net8.0
$env:TELEMETRY_FIXTURE = "$PWD/docs/tasks/TASK-20261003-TELEMETRY-V2/client-net8.json"
node --test scripts/tests/telemetry-worker.test.mjs
```

Node harness dùng `node:sqlite` có sẵn trong Node 24 tại máy kiểm tra. Test C# ghi local JSONL vào thư mục temp riêng, không ghi vào diagnostics sản phẩm. Không có request mạng ngoài loopback trong harness.

Các bước nghiệm thu runtime còn lại: áp dụng schema/index vào D1 đích; deploy Worker; khởi động lại Revit với đúng DLL; xác nhận receipt và correlation trên model thật; đo full callback P95/P99/max, queue/drop counters và heap khi vẽ/trim/undo, exception và mất mạng. Build 2023/2026 không thay cho kiểm tra runtime từng phiên bản Revit 2020–2026.

Chi tiết contract, giới hạn và thứ tự rollout: [DocumentTelemetry.md](../../DocumentTelemetry.md).
