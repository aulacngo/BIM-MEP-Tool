# Kiến trúc BIM TOOL: Telemetry, dữ liệu CAD và CAD-to-BIM Generative Engine

> Trạng thái: kiến trúc mục tiêu và lộ trình triển khai  
> Phạm vi: Revit Client cho net48 và net8.0-windows, Cloudflare ingestion/analytics, CAD-to-BIM pipeline  
> Nguyên tắc không thương lượng: mọi truy cập Revit API xảy ra trên Revit UI thread; telemetry không được làm chậm hoặc thay đổi kết quả lệnh của kỹ sư.

## Tóm tắt quyết định

BIM TOOL không thu toàn bộ hình học hoặc mọi thao tác Revit. Hệ thống sử dụng hai tầng dữ liệu:

1. **L1 Operational Telemetry**: ghi cho mọi terminal command event, nhỏ, chuẩn hóa, đủ đo adoption, failure và latency.
2. **L2 Semantic/Spatial Trace**: action episode theo thứ tự: ý định + trạng thái/trói buộc trước thao tác → algorithm/hành động → trạng thái sau commit → validation/correction. L2 được ghi 100% khi failure; success chỉ khi opt-in hoặc lấy mẫu có kiểm soát, mặc định không quá 10%.

~~~mermaid
flowchart TD
    UX["Đỉnh 1: Trải nghiệm kỹ sư<br/>Revit UI không bị chặn"]
    DATA["Đỉnh 2: Dữ liệu đủ giàu<br/>Semantic + spatial + topology"]
    COST["Đỉnh 3: Băng thông và chi phí<br/>Cloud/Server có kiểm soát"]
    CORE["Telemetry & CAD Intelligence Framework"]
    CLIENT["Tầng 1: Revit Client Data Update"]
    ENGINE["Tầng 2: CAD-to-BIM Generative Engine"]
    UX -->|"snapshot primitive, queue bị chặn"| CORE
    DATA -->|"L1 phổ quát + L2 theo episode"| CORE
    COST -->|"batch, gzip, queue, retention"| CORE
    CORE --> CLIENT
    CORE --> ENGINE
~~~

Mô hình này giải BIM Telemetry & AI-Data Trilemma: UI Revit ưu tiên tuyệt đối; L2 tạo dữ liệu AI dùng được; toàn bộ flow batch, sample, queue và retention để chi phí không tăng theo toàn bộ model size.

Telemetry là best-effort observability, không phải audit trail pháp lý và không được là dependency của transaction Revit. Nếu cloud, queue hoặc local spool lỗi, thao tác BIM vẫn kết thúc theo hành vi hiện hữu; hệ thống chỉ ghi health/loss counters.

## Nền hiện hữu và ranh giới cần giữ

| Thành phần | Vai trò hiện hữu | Mở rộng đề xuất |
|---|---|---|
| DevCommandProxy | Bao bọc command và phát command diagnostics | Thêm command context, catalog metadata, correlation ID |
| CommandDiagnostics | Snapshot Revit, local JSONL, terminal event nền | Shared bounded batch dispatcher, cap selection capture |
| TelemetryHttpTransport | HTTPS, TLS/proxy, API key, timeout, silent failure | HTTP layer thấp nhất; không biết Revit API |
| DocumentTelemetryTracker | DocumentChanged read-only, primitive snapshot, bounded aggregate | Chỉ activity signal, không suy diễn source command |
| FlexPipeDiagnostics | JSONL cục bộ phục vụ debug | Map mốc quan trọng thành L2, vẫn giữ diagnostics offline |

Các fact trên là source-level, không là bằng chứng runtime. Build/harness xanh chỉ chứng minh source/regression. Mỗi phase phải xác minh riêng: DLL đúng được load, Revit thật không lag trên model thật, Worker nhận event, Queue/D1/R2 persist đúng và dashboard tái lập chính xác.

---

# 1. Giải Tam giác ràng buộc cốt lõi

## 1.1 Đỉnh 1 — Trải nghiệm kỹ sư: không chặn Revit UI

Revit API là single-threaded. Đưa Revit API vào Task.Run không làm nhanh hơn và có thể gây thread violation hoặc state không an toàn. Ngược lại, geometry scan, serialization, GZip, file I/O hoặc HTTP trên UI thread sẽ làm UI giật.

### Hợp đồng UI thread

Trên UI thread chỉ cho phép:

- tạo stable event ID, timestamp monotonic, client sequence number;
- đọc element/connector đã biết từ command, không scan toàn document;
- copy primitive/string đã cap: ID, category, family/type, XYZ, profile/dimension, bounding box, outcome;
- đóng gói DTO immutable không chứa Document, Element, Connector, UIApplication, XYZ sống hoặc closure quay lại Revit API;
- TryEnqueue không chờ. Queue đầy thì bỏ optional trace và tăng client_drop_count.

Không cho phép:

- HTTP, DNS, GZip, JSON serialization lớn, retry, lock dài, synchronous file I/O;
- FilteredElementCollector diện rộng chỉ để lấy thêm context;
- R-Tree build, CAD parsing, LLM request, D1 query;
- Wait, Result hoặc blocking semaphore.

### Ngân sách đo được, không hứa tuyệt đối

Không callback .NET/Revit nào đảm bảo tuyệt đối dưới 1 ms vì API call, allocation/GC, scheduler và model state không preemptible. Vì vậy dưới 1 ms là **ngân sách L1**, không phải lời hứa. Mọi giới hạn đo bằng Stopwatch trên Revit thật.

| Đường đi | Mục tiêu p95 | Guard bắt buộc |
|---|---:|---|
| L1 command capture | ≤ 0.25 ms | tối đa 8 selected IDs/categories |
| L2 known-scope capture | ≤ 1 ms | 2 primary elements, 8 connectors, 16 neighbor summaries |
| DocumentChanged | giữ cap sample 5 added/modified | không làm semantic action source |
| Enqueue | O(1), non-blocking | bounded queue, không retry UI |
| Dispatch | 0 ms UI block | background only, 1 in-flight sender |

Khi capture vượt TraceCaptureBudget, optional enrichment bị bỏ và ghi vào quality.dropped_optional_fields. SLO công bố phải có p50/p95/p99, drop count và benchmark: 500/5.000 selection, link lớn, mass delete, purge, link reload, undo/redo, cold start, offline/slow network.

~~~mermaid
sequenceDiagram
    participant U as Kỹ sư/Revit UI thread
    participant C as Command instrumentation
    participant Q as Bounded queue
    participant W as Single batch worker
    participant CF as Cloudflare Worker
    U->>C: Execute, failure hoặc commit
    C->>C: Đọc tối thiểu Revit API, copy primitive
    C->>Q: TryEnqueue snapshot, không chờ
    Note over C,Q: Queue đầy: bỏ L2 optional,<br/>tăng drop counter; BIM command tiếp tục
    U-->>U: UI tiếp tục ngay
    W->>Q: Flush 5–10 s, 10 event hoặc byte guard
    W->>W: Serialize và GZip nếu batch > 1 KiB
    W->>CF: POST batch với timeout hữu hạn
    CF-->>W: 202 Accepted và receipt ID
~~~

~~~csharp
// Không có Autodesk.Revit object trong envelope.
internal sealed record TelemetryEnvelope(
    string SchemaVersion, string EventId, string SessionId, long ClientSequence,
    DateTimeOffset OccurredAtUtc, string Tier, string ToolId, string Stage,
    CommandOutcome Outcome, CommandContext Context, object Payload);

internal static void CaptureTerminalOnRevitThread(
    ExternalCommandData commandData, CommandContext context, Result result, string message)
{
    var start = Stopwatch.GetTimestamp();
    var l1 = CommandSnapshotFactory.CreateL1(commandData, context, result, message);
    TelemetryRuntime.TryEnqueue(l1);
    if (TracePolicy.ShouldCapture(context, l1))
    {
        // Chỉ read known IDs; method trả primitive DTO hoặc null.
        var l2 = TraceSnapshotFactory.TryCreateL2(context, start);
        if (l2 != null) TelemetryRuntime.TryEnqueue(l2);
    }
}
~~~

Serialization, compression, local spool và network chỉ bắt đầu sau TryEnqueue. Worker không gọi Revit API dưới bất kỳ hình thức nào.

## 1.2 Đỉnh 2 — Dữ liệu semantic và spatial đủ giàu cho AI

AI không học được cách dựng MEP từ aggregate như “5 pipes và 3 fittings thay đổi”. Training/evaluation unit phải là:

~~~text
Intent + before-state + constraints
  -> operation/parameters + algorithm version
  -> transaction result + after-state
  -> validation + user correction/undo
~~~

| Thuộc tính | L1 Operational Telemetry | L2 Semantic/Spatial Trace |
|---|---|---|
| Tần suất | 100% terminal command event; document activity theo policy | failure 100%; success sampled ≤10% hoặc opt-in |
| Mục tiêu | reliability, adoption, latency, anomaly | debugging kỹ thuật, evaluation set, future learning |
| Payload | mục tiêu ≤ 1 KiB trước batch | 2–24 KiB sau cap; geometry lớn dùng blob_ref |
| Semantic | tool, stage, result, reason code, timing | family/type, systems, profiles, connectors, topology |
| Spatial | không hoặc local summary | XYZ, BasisZ, bbox, 5 m neighborhood |
| Privacy | pseudonymous default | opt-in/project policy, redaction |
| Storage | D1 facts/rollups | D1 metadata/index, R2 raw payload/geometry |

Sampling không ngẫu nhiên mù. Dùng deterministic hash theo project pseudonym, tool ID và event ID để replay được; quota theo tool/outcome; failure, conflict và user correction ưu tiên hơn random success.

Canonical numeric unit là mm. Debug Revit internal feet, nếu cần, nằm ở nhánh debug-only; không dùng localized display string làm canonical AI data.

| Nhóm | Capture có giới hạn | Mục đích |
|---|---|---|
| Element semantic | category, family, type, system, level, parameter allowlist | BIM/MEP context |
| Connector | role, origin, BasisZ, domain, flow, profile, dimensions | matching/topology |
| Profile | Round/Rectangular/Oval, diameter hoặc width x height, slope/length | rules/route |
| Spatial | local XYZ, bbox, transform, elevation, view type, 5 m summary | note/geometry, clash |
| CAD | link hash, block, layer, insertion, primitive ref, text annotation reference | CAD-to-BIM pairs |
| Outcome | result, reason, transaction, created/changed IDs, validation | call khác commit/correct |
| Feedback | undo/correction/replacement, optional rating | negative/correction examples |

Spatial neighborhood không phải full list trong bán kính 5 m. Nó cap 16 neighbors, ưu tiên relevant domain/category và chỉ giữ distance_mm, bbox, category/type, relation candidate, score. Full geometry chỉ vào offline evaluation workflow có consent và trace chỉ giữ blob_ref.

### Schema versioning

Mọi event có schema_version semantic version. Breaking change tăng major; field optional tăng minor. Server nhận current và previous major trong deprecation window, sau đó trả schema_unsupported mà client không retry vô hạn.

#### L1 command event

~~~json
{
  "schema_version": "1.0",
  "event_id": "01J...-uuidv7",
  "event_type": "command.terminal",
  "tier": "L1",
  "occurred_at_utc": "2026-10-02T08:12:31.312Z",
  "client": {
    "install_id_hash": "sha256:...",
    "session_id": "01J...",
    "sequence": 1842,
    "revit_major": 2026,
    "runtime": "net8.0-windows",
    "add_in_version": "2.4.0"
  },
  "project": {
    "project_id_hash": "sha256:...",
    "model_revision": "local:286",
    "view_type": "FloorPlan"
  },
  "command": {
    "tool_id": "mep.move_connect",
    "stage": "terminal",
    "outcome": "failed",
    "reason_code": "ORIENTATION_MISMATCH",
    "duration_ms": 842,
    "ui_capture_us": 182
  },
  "context": {
    "selection_count": 2,
    "selected_category_ids": [-2008044, -2008010],
    "active_level_id": 123456
  },
  "quality": {
    "sampled": false,
    "dropped_optional_fields": [],
    "client_drop_count_since_start": 0
  }
}
~~~

#### L2 MEP action episode

~~~json
{
  "schema_version": "1.0",
  "event_id": "01J...-uuidv7",
  "event_type": "mep.action_episode",
  "tier": "L2",
  "parent_event_id": "01J...-l1-terminal",
  "correlation_id": "01J...-command-run",
  "episode": {
    "tool_id": "mep.connect_sprinkler_flex_multi",
    "intent": "connect_sprinklers_to_branch",
    "algorithm_version": "flex-3point-v1",
    "mode": "branch_tee_elbow",
    "outcome": "succeeded",
    "duration_ms": 1564
  },
  "coordinate_frame": {
    "kind": "project_local",
    "unit": "mm",
    "cad_link_transform": [1,0,0,0,1,0,0,0,1,1240,-860,0]
  },
  "before": {
    "target_branch": {
      "element_id": 527381,
      "category": "Pipes",
      "system_name": "Fire Protection",
      "diameter_mm": 50,
      "connectors": [{
        "role": "open_branch_end",
        "origin_mm": [1250,740,3150],
        "basis_z": [0,0,-1],
        "shape": "Round",
        "diameter_mm": 50,
        "flow_direction": "Bidirectional"
      }]
    },
    "constraints": {"max_route_points": 3, "avoid_clash": true, "neighborhood_radius_mm": 5000}
  },
  "action": {
    "route_control_points_mm": [[1250,740,3150],[1450,740,3000],[1650,740,2700]]
  },
  "after": {
    "created": [{"category": "Flex Pipes", "element_id": 527711, "developed_length_mm": 611}],
    "connections_valid": true,
    "points_count": 3
  },
  "validation": {"transaction_status": "Committed", "clash_count": 0, "user_undo_within_60s": false}
}
~~~

Correction không sửa episode cũ. Nó là immutable event với parent_episode_id và reason như vertex_deleted, point_moved, flex_replaced, element_deleted, undo hoặc unknown_correction. Điều này giữ chronology và tạo feedback data đáng tin cậy.

## 1.3 Đỉnh 3 — Băng thông và chi phí Cloud/Server

| Rule | Giá trị khởi điểm | Lý do |
|---|---|---|
| Flush interval | jitter quanh 5–10 giây | giảm burst đồng bộ |
| Event threshold | 10 envelopes | gửi sớm khi active |
| Byte threshold | 48 KiB uncompressed | cap batch, trace lớn thành blob |
| Compression | GZip khi JSON batch > 1 KiB và có lợi | giảm bandwidth |
| In-flight | 1 sender/process | giảm fan-out, giữ ordering gần đúng |
| Retry | max 3, exponential backoff + jitter, background | không chặn UI, không loop 4xx |
| Local spool | byte cap + TTL, encrypted/ACL nếu chứa L2 | không làm đầy disk |
| Queue priority | L1 terminal/failure trước L2 success | degrade graceful |

Với 20 active clients, request rate khởi điểm xấp xỉ users / upload_interval: khoảng 4 request/s ở 5 giây hoặc 2 request/s ở 10 giây trước khi loại inactive. Batching giảm round trips, nhưng không tự giảm D1 row writes nếu consumer vẫn insert từng event.

~~~mermaid
flowchart LR
    RC["Revit Client<br/>L1/L2 bounded batch"]
    ING["Worker: /v1/telemetry/batches<br/>auth, schema, size, rate limit"]
    Q["Cloudflare Queue<br/>telemetry-ingest"]
    CONS["Queue Consumer<br/>idempotency + normalize"]
    D1["D1<br/>facts, dimensions, aggregates"]
    R2["R2<br/>L2 raw JSON / geometry blobs"]
    KV["KV<br/>kill switch, config, rate limit/cache"]
    DASH["Analytics API / Dashboard"]
    CRON["Scheduled retention + rollup"]
    RC -->|HTTPS GZip| ING
    ING --> KV
    ING -->|accepted refs| Q
    Q --> CONS
    CONS --> D1
    CONS --> R2
    DASH --> D1
    CRON --> D1
    CRON --> R2
~~~

Ingest Worker chỉ trả 202 Accepted khi payload qua validation và enqueue thành công, không chờ D1/R2. Nó xác thực ingest-only credential, content encoding, size, schema version, event ID, sequence shape và tool allowlist. Không log secret hay full trace vào Worker logs.

Queues có at-least-once delivery. event_id là idempotency key/primary key; consumer dùng insert-or-ignore/upsert idempotent. Không giả định global order: lưu occurred_at_utc, received_at_utc và client.sequence. D1 giữ facts/rollups; R2 giữ raw L2/geometry blobs; KV chỉ kill switch/config/rate-limit/cache ngắn, không là event source of truth.

~~~sql
CREATE TABLE telemetry_event (
  event_id TEXT PRIMARY KEY,
  occurred_at_utc TEXT NOT NULL,
  received_at_utc TEXT NOT NULL,
  project_hash TEXT NOT NULL,
  install_hash TEXT NOT NULL,
  session_id TEXT NOT NULL,
  client_sequence INTEGER NOT NULL,
  tool_id TEXT NOT NULL,
  event_type TEXT NOT NULL,
  tier TEXT NOT NULL,
  outcome TEXT,
  reason_code TEXT,
  duration_ms INTEGER,
  payload_ref TEXT,
  payload_sha256 TEXT,
  schema_version TEXT NOT NULL
);
CREATE INDEX ix_event_tool_time ON telemetry_event(tool_id, occurred_at_utc);
CREATE INDEX ix_event_project_time ON telemetry_event(project_hash, occurred_at_utc);
CREATE INDEX ix_event_failure ON telemetry_event(tool_id, outcome, occurred_at_utc);
CREATE TABLE telemetry_rollup_hour (
  hour_utc TEXT NOT NULL,
  tool_id TEXT NOT NULL,
  outcome TEXT NOT NULL,
  reason_code TEXT NOT NULL DEFAULT '',
  event_count INTEGER NOT NULL,
  duration_sum_ms INTEGER NOT NULL,
  duration_p95_hint_ms INTEGER,
  PRIMARY KEY(hour_utc, tool_id, outcome, reason_code)
);
~~~

Index tăng row write khi cột indexed thay đổi, nhưng giảm full scan/read dài hạn. Dashboard đọc rollup trước; raw event query bắt buộc time range, indexed filter, cursor và page cap. Không expose D1/SQL endpoint trực tiếp ra client.

Tại thời điểm 2026-10-02, Workers Free và D1 Free có daily limits; D1 có thể từ chối query sau quota. Kiểm tra [Workers pricing](https://developers.cloudflare.com/workers/platform/pricing/) và [D1 pricing](https://developers.cloudflare.com/d1/platform/pricing/) trước rollout vì limits/pricing thay đổi.

Cost guardrails:

- L1 rollup ưu tiên raw success detail; L2 success sampled và TTL.
- Không index mọi cột; dùng EXPLAIN QUERY PLAN với query dashboard thật.
- L1 raw giữ 30–90 ngày, hourly rollup 13 tháng, L2 raw 7–30 ngày theo consent; evaluation set promote explicit.
- Alert rows_read, rows_written, storage, Worker/Queue request, queue lag, 4xx/5xx, spool/drop, compression ratio.
- Khi budget guard hit: giảm success sampling, vẫn giữ failure summary; không tắt telemetry silently.

---

# 2. Khung tầng 1 — Data Update & Telemetry Framework cho Revit Client

## 2.1 Standard schema cho 75 tools

Mỗi tool khai báo metadata ở TelemetryCatalog. Shared runtime làm context, sampling, batching, transport và local diagnostics; tool không biết Cloudflare.

~~~mermaid
flowchart TD
    TOOL["75 BIM TOOL commands"]
    PROXY["DevCommandProxy / direct adapter"]
    CATALOG["TelemetryCatalog<br/>tool ID, domain, capture profile"]
    SNAP["Snapshot factories<br/>UI thread, primitive only"]
    RUNTIME["TelemetryRuntime<br/>priority queue, batch, spool, sender"]
    LOCAL["Local JSONL diagnostics"]
    HTTP["TelemetryHttpTransport"]
    TOOL --> PROXY
    TOOL --> SNAP
    PROXY --> CATALOG
    CATALOG --> SNAP
    SNAP --> RUNTIME
    SNAP --> LOCAL
    RUNTIME --> HTTP
~~~

~~~csharp
internal sealed record ToolTelemetryDefinition(
    string ToolId,
    string DisplayName,
    string Domain,
    TraceProfile TraceProfile,
    bool CaptureFailureTrace,
    double SuccessSampleRate,
    IReadOnlySet<string> AllowedReasonCodes);

// cad.place_family, mep.move_connect, mep.sprinkler_flex_pipe,
// mep.sprinkler_flex_multi, mep.draw_multi_pipe, mep.avoid_clash
~~~

Mỗi tool bắt buộc có stable tool_id, stage contract, reason-code allowlist, correlation_id, episode_id nếu L2, best-effort model revision và capture cap/budget. Stage chuẩn: started, input_ready, preflight_failed, transaction_started, committed, validation_failed, cancelled, failed. Message/stack trace là local/redacted debug, không dashboard dimension.

DevCommandProxy vẫn là generic wrapper. Command nào gọi CommandDiagnostics trực tiếp cũng phải đi qua TelemetryRuntime; không để CommandDiagnostics, DocumentTelemetryTracker và FlexPipe tự tạo ba queue nền độc lập.

Selection không phải global telemetry mặc định. Tool chỉ capture selection IDs mà chính nó dùng. DocumentChanged là observer sau commit/undo/redo, không tự khẳng định command nào sửa model. Chỉ correlate khi command context active, known IDs/transaction name phù hợp, timestamp trong window và confidence được lưu. Nếu thiếu bằng chứng, document event độc lập.

## 2.2 L2 lifecycle: before, after, validation

~~~mermaid
stateDiagram-v2
    [*] --> IntentCaptured
    IntentCaptured --> PreflightFailed: input or rule invalid
    IntentCaptured --> BeforeSnapshotted
    BeforeSnapshotted --> TransactionStarted
    TransactionStarted --> RolledBack: exception or cancellation
    TransactionStarted --> Committed
    Committed --> AfterSnapshotted
    AfterSnapshotted --> Validated
    Validated --> CorrectionLinked: undo or edit observed
    PreflightFailed --> Published
    RolledBack --> Published
    Validated --> Published
    CorrectionLinked --> Published
~~~

Before/after đều capture UI thread theo known IDs. TransactionStatus Committed không đồng nghĩa validation pass. Invariant phải cụ thể: expected created count, connector connected, profile/system match, no known clash, points count, correct level/location.

## 2.3 PlaceFamily — CAD-to-BIM placement trace

PlaceFamily hiện có CAD link/type, block scan, CAD unit, calibration delta, deduplicate XY, existing-instance filtering, level, elevation, family/type, created IDs và view-range warning. Đây là L2 CAD placement đầu tiên.

| Stage | L1 bắt buộc | L2 detail |
|---|---|---|
| Input | tool, import/link kind, result | link hash, transform, source unit, owner view, Xref count/state |
| Block scan | raw/unique/to-place/skipped count, elapsed | block, layer, insertion samples ≤16, duplicate classification, scan diagnostics |
| Mapping | family/type, level, elevation | IDs, active view/range summary, calibration delta, conversion policy |
| Commit | created count, transaction result | created IDs, local XYZ/level sample, per placement validation |
| Visibility | view range warning code | view crop/range summary; no screenshot default |

~~~json
{
  "episode": {"tool_id": "cad.place_family", "intent": "place_revit_family_from_cad_block", "outcome": "succeeded"},
  "cad": {
    "link_id_hash": "sha256:...",
    "block_name": "SPRINKLER_PENDANT",
    "layer_name": "FP-SPRK",
    "source_unit": "Millimeters",
    "transform_to_project": {"translation_mm": [1240,-860,0]},
    "scan": {"raw": 304, "unique": 301, "duplicates": 3, "xref": {"total": 1, "resolved": 0}}
  },
  "mapping": {"family": "Sprinkler Pendant", "type": "K80 15mm", "level": "L2", "elevation_offset_mm": 2700},
  "placement": {"requested_count": 301, "skipped_existing": 265, "created_count": 36},
  "validation": {"created_count_matches_request": true, "view_range_warning": true}
}
~~~

Không gửi raw DWG path, project title, username hoặc raw text note mặc định. Dùng hash/pseudonym. Xref unresolved là CAD_XREF_UNRESOLVED, không phải kết luận placement algorithm lỗi.

## 2.4 MoveConnect — connector matching giải thích được

MoveConnectCmd đã tính shared domain, distance, BasisZ dot product, shape, dimension và flow compatibility. L2 chuyển diagnostic string thành structure.

Four-point diagnostic được định nghĩa cố định:

1. target_connector.origin;
2. source_connector.origin_before_move;
3. source_connector.origin_after_move, kỳ vọng trùng target trong tolerance;
4. selected_candidate.normal BasisZ và angle/dot với target normal.

Point 1–3 là positions; point 4 là directional diagnostic, không phải tọa độ.

| Nhóm check | Fields | Reason code |
|---|---|---|
| Selection/topology | target/source category, unused connector count, shared domain | TARGET_NO_UNUSED_CONNECTORS, SOURCE_NO_UNUSED_CONNECTORS, DOMAIN_MISMATCH |
| Candidate ranking | count, rank, score, distance, dot, angle | NO_MATCHING_CONNECTOR_PAIR, ORIENTATION_MISMATCH |
| Profile | shape, diameter/radius hoặc width x height, tolerance | PROFILE_MISMATCH |
| Flow | source/target flow, compatibility | FLOW_DIRECTION_CONFLICT |
| Mutation | move vector, transaction/connect result | MOVE_FAILED, CONNECT_FAILED, COMMIT_FAILED, CONNECTED |

~~~json
{
  "episode": {"tool_id": "mep.move_connect", "intent": "move_and_connect", "outcome": "failed"},
  "candidate": {
    "candidate_count": 4, "selected_rank": 1, "score": 3184,
    "distance_mm": 86.3, "normal_dot": 0.12, "angle_deg": 83.1,
    "target": {"origin_mm": [1200,800,2700], "basis_z": [1,0,0], "shape": "Rectangular", "size_mm": [400,200], "flow": "Out"},
    "source_before": {"origin_mm": [1114,800,2700], "basis_z": [0,1,0], "shape": "Rectangular", "size_mm": [400,200], "flow": "Out"}
  },
  "validation": {"reason_code": "ORIENTATION_MISMATCH", "transaction_started": false}
}
~~~

L2 lưu candidate được chọn và reason rejection, không gửi mọi connector của model.

## 2.5 Sprinkler Flex Pipe và Flex Multi

Tên class hiện hữu là ConnectSprinklerFlexPipeCmd và ConnectSprinklerFlexPipeMultiCmd; schema dùng tool IDs mep.sprinkler_flex_pipe và mep.sprinkler_flex_multi.

| Pha | Capture |
|---|---|
| Intent/selection | preselection hay interactive, mode Elbow/Branch-Tee-Elbow, pipe/fitting/sprinkler counts |
| Before | system name/type, pipe diameter, level, open connectors, sprinkler family/type/K-factor, branch identity, neighborhood |
| Planning | branch-end candidates, assignment summary, chosen pair/cost, max points, route strategy |
| Action | control points, tangents nếu set, created pipe/flex/fitting, transaction group result |
| Validation | endpoint connection, Points.Count, developed/chord length, clash, leftover stub, undo/correction |

Branch identity phải dựa topology/root pipe/selection scope, không nearest pipe toàn model. Với FlexPipe 3 points, source-of-truth là start, middle, end. Nếu Revit không tạo route đúng profile, rollback subtransaction và phát ROUTE_PROFILE_UNSATISFIED; không silent fallback về hai points.

~~~json
{
  "episode": {"tool_id": "mep.sprinkler_flex_multi", "mode": "branch_tee_elbow", "algorithm_version": "flex-3point-v1", "outcome": "succeeded"},
  "topology": {
    "branch_root_element_id": 501120,
    "open_terminal_count": 1,
    "candidate_branch_end_count": 3,
    "selected_branch_end": {"origin_mm": [1250,740,3150], "basis_z": [0,0,-1]}
  },
  "assignment": {"objective": "minimum_total_distance", "pair_count": 4, "chosen_cost_total_mm": 1882},
  "route": {
    "profile": "three_point_gravity_v1",
    "control_points_mm": [[1250,740,3150],[1450,740,3000],[1650,740,2700]],
    "points_count_after_regeneration": 3
  },
  "validation": {"endpoints_connected": true, "clash_count": 0, "leftover_stub_count": 0}
}
~~~

Parameter allowlist có thể gồm system name/type, pipe diameter, curve length, fitting part type và sprinkler K-factor. Mọi parameter resolve UI thread rồi copy bounded numeric/string. Missing có availability = unavailable, không gửi 0.

## 2.6 Automated anomaly detection

Analytics chỉ phát tín hiệu để kỹ sư điều tra, không tự kết luận tool hoặc người dùng sai.

~~~mermaid
flowchart LR
    E["L1/L2 events"] --> N["Normalize + idempotent facts"]
    N --> H["Hourly rollups + session features"]
    H --> R["Rules + robust baseline"]
    R --> A["Alert + dashboard triage"]
    A --> V["Engineer validates cohort/version/payload"]
~~~

| Tín hiệu | Cách tính | Alert | False-positive control |
|---|---|---|---|
| Rage clicks | cùng session + tool, repeated start/cancel/fail, không success xen giữa | ≥3 trong 30 s hoặc vượt p99 baseline | loại retry workflow/shortcut, xem outage cohort |
| Failure spike | failed / terminal theo tool/version/Revit/model cohort | denominator ≥20 và vượt 7/28 day Wilson hoặc robust z baseline | cancelled không là failure; compare đúng cohort |
| UX bottleneck | p95 duration, preflight retry, selection loop, time-to-success | p95 > 2x baseline đủ sample | censor offline/network, review UI/release |
| Semantic mismatch | reason-code distribution đột biến | significant orientation/Xref increase | inspect sampled L2 IDs, không raw personal data |
| Pipeline loss | queue drop, spool expiry, 4xx/5xx, sequence gap | sustained nonzero > 5 min | telemetry health tách product failure |

Baseline partition theo tool ID, add-in version, Revit major, outcome và model cohort. Alert nêu numerator, denominator, window, baseline, loss rate và anonymized event IDs.

---

# 3. Khung tầng 2 — CAD-to-BIM Generative Engine

AI layer tạo plan typed có evidence, không tự gọi Revit API hoặc tự commit.

~~~mermaid
flowchart LR
    CAD["DWG/DXF hoặc Revit ImportInstance"]
    P1["Layer 1: CAD Primitives Extraction"]
    P2["Layer 2: Spatial Index + R-Tree"]
    P3["Layer 3: AI Reasoning + Topology"]
    PLAN["Typed BIM Plan: confidence + evidence"]
    PRE["Preview và user approval"]
    P4["Layer 4: Revit construction adapter"]
    REVIT["TransactionGroup + validation"]
    TEL["L2 execution/correction trace"]
    CAD --> P1 --> P2 --> P3 --> PLAN --> PRE --> P4 --> REVIT --> TEL
    TEL -.evaluation feedback.-> P3
~~~

## 3.1 Layer 1 — CAD primitives extraction

Output normalized gồm lines, arcs, polylines/spline approximations; blocks và insertion transforms; layers/colors/linetypes nếu có semantic; text/mtext/leaders/dimensions; extents/source units/transform/Xref state; stable primitive IDs và provenance gồm link/file hash, handle, block/layer, parser version.

Khi source là ImportInstance/CAD link trong Revit, Revit API extraction chỉ chạy UI thread hoặc external-event execution. Background chỉ xử lý DTO primitives đã copy. Headless là adapter out-of-process riêng, ví dụ approved DWG parser hoặc RevitCoreConsole workflow, cần licensing, compatibility, isolation/security review; không phải Task.Run gọi Revit API.

Normalize về project_local_mm; lưu source unit và transform explicitly. Không trộn CAD mm với Revit internal feet. Parser external input cap entity/text/file complexity, sandbox process và trả reason code cho malformed/oversize data.

~~~json
{
  "primitive_id": "cad:linkhash:handle:1A2B",
  "kind": "block_instance",
  "layer": "FP-SPRK",
  "block_name": "SPRINKLER_PENDANT",
  "insertion_mm": [1250,740,2700],
  "rotation_deg": 0,
  "bbox_mm": {"min": [1200,690,2700], "max": [1300,790,2750]},
  "source": {"link_hash": "sha256:...", "handle": "1A2B", "parser_version": "cad-extract-1.0"}
}
~~~

## 3.2 Layer 2 — Spatial indexing, clustering và R-Tree

Layer 2 xây evidence graph, không tự hiểu MEP:

1. Insert primitive bounding box vào R-Tree theo project-local mm.
2. Text, leader, dimension và block query local radius mặc định 5.000 mm.
3. Rank theo leader endpoint/line-of-sight, layer compatibility, direction, distance, elevation agreement và discipline dictionary.
4. Tạo weighted candidate relations: annotation_describes_block, polyline_is_pipe_centerline, text_is_size, text_is_elevation.
5. Score thấp/ambiguous giữ top-k candidates, không auto attach.

~~~mermaid
flowchart TD
    TXT["Text: DN50 hoặc EL+2700"]
    RT["R-Tree query, max 5.000 mm"]
    GEOM["Lines, polylines, blocks"]
    SCORE["Relation scorer: leader, layer, distance, direction"]
    GRAPH["Spatial evidence graph: nodes + weighted edges"]
    TXT --> RT
    GEOM --> RT
    RT --> SCORE --> GRAPH
~~~

Nearest-only không đủ: text có thể là legend/note hoặc áp dụng nhiều geometry. relation_confidence, evidence array và ambiguous là first-class field.

## 3.3 Layer 3 — AI reasoning và topology synthesis

| Năng lực | Input | Output kiểm chứng |
|---|---|---|
| Nhận diện system MEP | primitives, layers, notes, evidence graph | system hypotheses, confidence, cited primitive IDs |
| Suy missing fittings | centerline graph, size/elevation/connector rules | tee/elbow/reducer candidates và constraints |
| Reconcile elevation | text elevation, block type, level/view | normalized elevation, conflict list |
| Family mapping | CAD block + geometry + annotations | ranked approved family/type candidates |
| Route proposal | terminals, obstacles, clearance/rules | control points và fittings |

LLM dùng cho semantic normalization, ambiguity explanation, plan proposal; graph algorithm/GNN cho connectivity/topology. Dù model nào, deterministic validation bắt buộc: domain/system/profile compatibility; connector direction/flow; dimensions/tolerance; level/elevation; no clash dưới declared clearance; family/type ở approved catalog; confidence threshold và human approval policy.

~~~json
{
  "plan_version": "1.0",
  "plan_id": "01J...",
  "source_graph_id": "cadgraph:...",
  "operations": [{
    "operation_id": "op-17",
    "kind": "place_family",
    "family_selector": {"approved_catalog_key": "fp.sprinkler.pendant.k80"},
    "level_selector": {"evidence": ["text:EL+2700", "view:L2"], "confidence": 0.88},
    "position_mm": [1250,740,2700],
    "evidence_ids": ["block:1A2B", "text:4F10"],
    "confidence": 0.91,
    "requires_human_approval": true
  }],
  "unresolved": [{
    "kind": "annotation_ambiguity",
    "candidate_ids": ["poly:72", "poly:73"],
    "reason": "one DN50 text matches two equidistant routes"
  }]
}
~~~

Plan thiếu evidence, policy result, approved catalog key hoặc confidence không được qua Layer 4.

## 3.4 Layer 4 — Revit construction và auto-connect

PlaceFamily, MoveConnect, DrawMultiPipe, AvoidClash và FlexPipe chứa thuật toán quan trọng nhưng nhiều command có interactive selection/TaskDialog. Generative engine không gọi trực tiếp IExternalCommand.Execute.

~~~text
Pure planning services, không Revit API
  -> typed Request + ValidationResult
Revit construction adapters, UI thread
  -> PlaceFamilyEngine / MoveConnectEngine / DrawMultiPipeEngine /
     AvoidClashEngine / FlexRouteEngine
  -> TransactionGroup + known IDs + post-commit validation
ExternalCommand/UI
  -> selection, preview, approval, invoke adapter
~~~

Default policy:

1. render preview/highlight, explain evidence;
2. user approve full plan hoặc operation group;
3. execute TransactionGroup với transaction theo group;
4. validate connector/profile/system/elevation/clash;
5. rollback group khi critical invariant fail;
6. emit L2 episode cho commit, rollback hoặc cancel.

Không phase nào cho phép LLM tự commit production model không preview, approval và validation policy.

---

# 4. Security, privacy, data quality và vận hành

## 4.1 Phân loại dữ liệu/consent

| Class | Ví dụ | Default |
|---|---|---|
| Operational pseudonymous | tool, duration, outcome, Revit major, hashed project/install | enabled nếu tổ chức duyệt |
| Sensitive model metadata | raw project title, username, DWG path, raw text | không upload, hash/redact |
| Semantic/spatial L2 | block/layer/family, local geometry, connector context | failure summary hoặc explicit project opt-in |
| Raw CAD/geometry blob | drawing fragment/full primitives | opt-in riêng, retention ngắn, controlled access |

- Pseudonym salt/project secret ở server/managed config, không hard-code client.
- Credential từng nhúng client phải coi exposed: rotate, ingest-only, rate-limited, revocable. Ingest key không đọc dashboard.
- Local credential dùng DPAPI CurrentUser nếu cần; không commit secret/raw trace/DWG path vào repo.
- Desktop client không cần broad CORS. Dashboard API cần allowlisted origins, explicit OPTIONS, role riêng, bounded cursor pagination và audit access.
- Exception/stack trace không là dashboard field; cap/redact local, dùng hash fingerprint để group.

## 4.2 Data quality

L2 field quan trọng có state: observed; derived kèm algorithm version; inferred kèm confidence/evidence; unavailable; hoặc redacted. Không dùng null/0 để lẫn missing, API failure và real zero.

## 4.3 Kill switch/failure modes

| Failure | Hành vi bắt buộc |
|---|---|
| Cloudflare timeout/5xx | BIM command không chờ; bounded retry/spool; health counter |
| 401/403 | stop retry nhanh; remote dispatch backoff; giữ local diagnostics |
| schema/size 400 | drop có reason, không retry loop; alert release owner |
| Queue full | L1 terminal/failure trước; L2 success drop trước; report count |
| Queue/D1/R2 lag | Worker accepted sau enqueue; consumer alert; không ảnh hưởng UI |
| Kill switch | ngừng remote dispatch, không ngừng BIM command |
| API capture error | degraded telemetry event; không làm command fail |

---

# 5. Lộ trình thực thi

## Phase 0 — Contract và baseline, 1–2 sprint

**Deliverables**

- TelemetryCatalog, schema v1, reason code catalog, retention/consent policy.
- Shared bounded batch runtime; không còn đường gửi nền độc lập.
- Golden JSON, overflow/retry/GZip/idempotency tests cho net48/net8.
- Baseline UI benchmark theo model/version.

**Exit gate**

- Không Revit object qua worker, enforced bằng DTO/test double.
- Hai target source-behavior synchronized, 0 warnings/0 errors.
- Ingest/read authorization và key rotation review complete.
- Product owner duyệt L2 consent/retention.

## Phase 1 — Enrich key tools, 2–3 sprint

Scope: PlaceFamily; MoveConnectCmd; ConnectSprinklerFlexPipeCmd; ConnectSprinklerFlexPipeMultiCmd.

Order: L1 terminal/catalog → structured reason code → L2 before/after known scope → failure 100% / success ≤10% → explicit correction link → hot-load, receipt correlation, real-model benchmark.

**Exit gate**

- Báo cáo p95/p99 Revit thật theo budget.
- L2 tái tạo outcome/validation representative test.
- Không raw project/DWG/user text cloud default.
- Drop/retry/spool/compression/sequence gap observable.

## Phase 2 — Cloudflare analytics/anomaly dashboard, 1–2 sprint

Scope: authenticated batch endpoint; Queue consumer idempotent; D1 facts/rollups; R2 lifecycle; KV config; dashboard adoption/failure/duration/rage click/pipeline health.

**Exit gate**

- Duplicate Queue delivery không tăng facts/aggregate.
- Dashboard dùng indexed filter, bounded cursor, không full scan.
- Soak test 20 active clients, offline/slow network và Cloud failure.
- Cost dashboard có D1 read/write, storage, Worker/Queue requests, retention effect.

## Phase 3 — CAD block/text parsing prototype, 2–3 sprint

Scope: bounded primitives cho một drawing family; blocks/layers/text/lines/polylines/transforms; R-Tree 5 m; evidence graph; CAD block to approved family catalog; review screen, chưa auto commit.

**Exit gate**

- Unit/transform round-trip đúng trên fixtures.
- Ambiguous annotation không auto bind; UI nêu candidates/evidence.
- Prototype tạo typed BIM Plan trước mutation.
- Parser cap malformed/large input và reason code.

## Phase 4 — Full auto-place/auto-route, nhiều sprint và gated

Scope: tách construction engine khỏi UI commands; preview/approval, TransactionGroup, validation/rollback; orchestrate PlaceFamily/MoveConnect/DrawMultiPipe/AvoidClash/Flex route; correction episodes đi qua human-reviewed evaluation workflow.

**Exit gate**

- Không auto commit ngoài approved family/rule/level/system catalog.
- 100% critical generated operation có before/after/validation L2 trace.
- Regression suite, correct DLL hot-load, real model validation cho mọi target/version support.
- Shadow mode so sánh plan với kỹ sư trước production; KPI: accuracy, undo/rework, failure, UI latency, cost/event.

---

# 6. Quyết định nhanh và checklist

| Câu hỏi | Quyết định |
|---|---|
| Quét full model cho AI? | Không. Known command scope + bounded neighborhood; full geometry chỉ controlled/opt-in offline. |
| Revit API ở ThreadPool? | Không bao giờ. Worker chỉ xử lý primitive DTO. |
| L1/L2 frequency? | L1 100% terminal; L2 failure 100%, success sampled/opt-in. |
| DocumentChanged cho source command? | Không tự suy diễn; chỉ correlate khi có evidence. |
| Gửi từng event ngay? | Không. Batch 5–10 s, 10 event, byte cap, one in-flight sender. |
| D1 giữ full geometry? | Không. D1 facts/index/rollups; R2 blob theo retention. |
| KV là event database? | Không. KV chỉ config/kill switch/rate limit/cache. |
| AI tự commit? | Không. AI tạo typed plan; adapter, preview, approval, validation mới mutate. |
| Zero lag tuyệt đối? | Không thể hứa tuyệt đối; SLO đo được, cap và benchmark. |

## Checklist trước khi bật telemetry cho tool mới

- [ ] Có stable tool ID, domain, version, reason-code catalog.
- [ ] Có correlation ID, terminal L1, duration.
- [ ] L2 profile khai báo known IDs, field caps, capture budget.
- [ ] Không DTO giữ Revit API object sau enqueue.
- [ ] Success/failure/cancel/rollback/validation có outcome semantics.
- [ ] DocumentChanged linkage, nếu có, ghi confidence/evidence.
- [ ] net48/net8 behavior/schema tương đương; shared source hoặc structural test.
- [ ] Golden JSON, overflow, GZip, timeout, retry, dedupe, redaction pass.
- [ ] Correct DLL hot-load, real-model benchmark, endpoint receipt, dashboard reconciliation verified.

## Bằng chứng cần phân biệt

| Bằng chứng | Chứng minh được | Chưa chứng minh được |
|---|---|---|
| Unit/harness | DTO/schema, no-thread violation với test doubles, batch logic | correct DLL, UI latency thật, Worker receipt |
| Build hai target | source compile | Revit/model runtime behavior |
| Local JSONL | client cố diagnostics | remote persistence |
| Worker 202 | Worker validate/enqueue | consumer/D1/R2 hoàn tất, semantic correctness |
| D1/R2/dashboard | persisted/queryable telemetry | BIM construction/AI inference đúng |
| L2 validation/correction | proof một operation | generalization project khác |

Kiến trúc này tạo đường có kiểm chứng từ telemetry nhẹ, an toàn đến CAD-to-BIM có evidence, human control và validation, thay vì đánh đổi UI Revit hoặc cloud budget cho dữ liệu không dùng được.
