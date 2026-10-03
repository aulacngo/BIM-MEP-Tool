# Telemetry & Diagnostics V2+

Implementation date: 2026-10-03. Applies to paired `net48` and `net8.0-windows` sources.

## Runtime architecture

```text
Revit main thread
  DevCommandProxy invocation scope -> bounded primitive command/L2 snapshot
  DocumentChanged -> bounded primitive change snapshot
             |
             v
AppDomain-shared outbox: <=200 items AND <=2 MiB accounted retained bytes
  capacity overload: evict oldest
  lock contention: drop incoming immediately, increment drop counter
             |
             v
One async ThreadPool consumer: <=5 items/batch, timer flush every 5 seconds
  JSON + SHA256 + local JSONL + GZip (>1 KiB) + HTTP
             |
             v
Worker: bounded decode -> normalize each event -> persist valid rows -> receipt
  L1/document.changed + L1/command.terminal + L2/telemetry.detail
             |
             v
D1 event_json + correlation expression indexes -> dashboard drill-down
```

The 200-item limit includes local-only diagnostics. Each item's owned strings/arrays
are capped before admission. The queue applies a conservative retained-size reservation,
not a serialized-JSON length estimate. Large events hit the byte cap before 200 items.
The raw JSON path is capped at 8,192 UTF-16 characters; each wire event is checked again
on the worker. At most five such strings plus bounded compression/HTTP buffers are in
flight. This is a bounded retained working set, not a promise of zero allocations or
an exact cap on process RSS/GC committed memory.

Only one timer and one consumer are shared across `Assembly.Load(byte[])` copies via
BCL-only AppDomain state. Queue entries contain method delegates targeting internal,
closed primitive snapshots; arbitrary caller factories are no longer accepted. No
Document, Element, ElementId, API enumerable, Exception or caller-owned object graph
is retained. This does not make Revit's loaded assemblies unloadable; it prevents
telemetry from adding one timer/outbox per hot-loaded copy.

The queue lock covers admission/removal only. UI admission uses `Monitor.TryEnter`
with no wait. At count/byte overload the oldest item is evicted. Under lock contention
the incoming item is dropped so the UI never waits for a descheduled lock owner.
A timer flushes a tail smaller than five. No retry/durable disk outbox is implemented:
a failed or timed-out in-flight batch is discarded, and newer events continue.
`DroppedCount` measures local admission/serialization loss;
`FailedDeliveryCount` is exposed in event quality as **unacknowledged delivery count**,
which can include events committed by a server before a response was lost.
Server 202 receipts contain rejected-event counts; the current best-effort HTTP client
only consumes HTTP success/failure, not individual receipt details.

Local JSONL uses the same bounded queue and rotates at approximately 4 MiB with one
previous file. It is best effort and can also lose entries under overload.
Shutdown unsubscribes/stops new document captures without waiting on HTTP; the shared
process-lifetime queue may finish pending work while the process remains alive.

## UI work and latency boundary

Normal callbacks capture counts, bounded strings and primitive IDs. They perform no
JSON parsing/serialization, SHA256, GZip, file writes or HTTP. `DocumentChanged` caps
transaction names at 8 x 128 characters, categories at 5 x 64 characters, and stops
starting optional category reads after its 0.1 ms capture budget is exhausted.
Command snapshots copy at most eight selected element IDs, without resolving elements.

Revit API calls remain on the main thread. `GetAddedElementIds`, selection collection
creation, one category lookup, JIT, GC and OS scheduling cannot be preempted by this
budget. Therefore **<0.2 ms is an acceptance target, not an established hard bound**.
Exception capture reads bounded type/message/callsite/stack text on the API thread;
stack materialization is explicitly an error-only latency exception, avoiding retention
of arbitrary Exception/Data graphs. Serialization and hashing still run on the worker.

`quality.capture_ms` on document events measures capture up to enqueue; measure the full
callback separately in real Revit. Synthetic harness timings are not real-model timings.

## Contract and deep signals

The V2 envelope retains `schema_version: "1.0"` for compatibility. New document events
use `tier: "L1"`, `event_type: "document.changed"` and
`command.tool_id: "DocumentChanged:" + Operation`. Each notification stays a separate
ordered event; counts are per-notification change occurrences, not unique model elements.

Document context includes added/deleted/modified counts, transaction names, raw operation,
normalized Committed/RolledBack/Undone/Redone/Unknown state, and sampled category IDs/names.
Rollback notifications with zero changed IDs are preserved. No state is inferred from
`Result.Failed`. Terminal command state is only the last DocumentChanged operation
observed inside its scope, explicitly labelled in `transaction_state_evidence`.
If Revit emits no observable operation, state remains `unobserved`.

Category completeness, inspected count, deleted-category unavailability and truncated
transaction names are explicit quality fields. Deleted elements are not resolved.
Sampling does not claim the entire category distribution of a large edit.

An active proxy scope gives `origin: bin_tool` with active-scope evidence. Outside that
scope, document events use `user_or_other_addin`; DocumentChanged alone cannot prove a
manual user action. Undo/redo performed later has its own event and is not falsely
attached to an earlier command. There is no heuristic cross-invocation correlation.

`BeginCommand` assigns an invocation ID and the eventual L1 terminal event ID before
execution, clears/restores scope in `finally`, and shares primitive thread-local context
with hot-loaded assemblies. L1/L2/document events share `correlation_id`; L2 additionally
carries `parent_event_id`. A shared session ID and sequence span loaded copies.
An unscoped direct caller is explicitly marked unscoped; it must use BeginCommand to
obtain invocation linkage. Async/background callers do not inherit a Revit thread scope.

Commands preserve duration_ms. Error L2 events include exception_type, exception_message,
revit_callsite (actual exception target, including BIN code where appropriate), bounded
stack_trace and capture_stage. Caught errors only have the depth supplied by their caller.
`WriteL2` accepts flat anonymous primitive fields, copies at most 32 fields/4,096 total
key/value string characters, rejects nonprimitive references, and marks truncation.
`WriteL2Json` accepts at most 4,096 characters and parses only in the background; malformed
JSON becomes an explicit parse_error detail rather than damaging a batch. MoveConnect's
name removal and reason extraction also run in that worker.

## Worker/D1 and dashboard

POST `/api/telemetry/batch` and `/` accept a single event or 1..500 events. The Worker
caps **decompressed UTF-8 bytes** at 1 MiB while streaming and caps each event at 64 KiB.
Malformed JSON/gzip or an invalid outer batch cannot be split and receives 400/413.
Within a valid JSON batch, malformed events are rejected individually with their input
index; valid neighbours still persist. An all-invalid event array also returns a 202
receipt with accepted=0 and explicit rejection reasons.

Legacy V1 command_name/details payloads are normalized. Missing client IDs receive a
server UUID and a quality flag; these legacy events cannot provide retry idempotency or
recover their original occurrence time. V2 event IDs use INSERT OR IGNORE idempotency.

Per-tier INSERT SELECT from json_each keeps D1 query counts bounded. A failed chunk is
retried row by row with a request-wide 20-attempt cap; remaining rows are marked retryable.
Receipts report accepted, inserted, duplicates, rejected/errors and retryable_indices.
Persistence failure returns 503 with partial results. Secondary anomaly analysis runs
through waitUntil and cannot turn successful persistence into failure. Document events
are excluded from command-failure metrics. Optional anomaly checks are capped at four
keys per request, so they are best effort under a large batch.

Correlation is stored in existing event_json. Two additive expression indexes in
`cloudflare/schema.sql` support both existing and new databases without ALTER/backfill.
GET `/api/telemetry/correlation?id=...` returns up to 200 linked events and a truncation
flag, requires X-API-Key matching TELEMETRY_READ_API_KEY, and uses parameter binding.
The dashboard's command buttons open this trace, including error/element details. Its
read key is entered by the operator and kept only in page memory. Existing dashboard
changes present before this task were preserved.

## Validation and remaining runtime gates

See [validation report](tasks/TASK-20261003-TELEMETRY-V2/VALIDATION.md), paired build logs,
client fixtures and executable harnesses in `scripts/tests/DocumentTelemetry`,
`scripts/tests/TelemetryTransport` and `scripts/tests/telemetry-worker.test.mjs`.

Validation is offline/source plus localhost HTTP. Worker deployment, remote D1 migration,
correct-DLL startup/hot-load, and HCM-MEP63 real-model P95/P99/max capture latency and
long-run retained memory remain unverified. The configured reference builds are Revit
2023/net48 and Revit 2026/net8; this is not an executed Revit 2020..2026 runtime matrix.

For deployment, apply schema.sql to the intended D1 database and deploy the Worker before
replacing the client DLLs. Restart Revit with the updated add-in to ensure the subscribed
tracker/proxy and shared outbox owner are the new implementation; an old already-loaded
V1 dispatcher cannot be replaced merely by loading a new command assembly.

## Primary references

- [Autodesk DocumentChanged and transaction events](https://help.autodesk.com/cloudhelp/2018/ENU/Revit-API/Revit_API_Developers_Guide/Basic_Interaction_with_Revit_Elements/Transactions/Transactions_in_Events.html)
- [Autodesk UndoOperation enum](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/d5c8c31a-3b69-48c0-feac-b176a54e7934.htm)
- [Cloudflare D1 batch transaction behavior](https://developers.cloudflare.com/d1/worker-api/d1-database/)
- [Microsoft HttpWebRequest](https://learn.microsoft.com/en-us/dotnet/api/system.net.httpwebrequest)
