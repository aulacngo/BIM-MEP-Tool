# DocumentChanged telemetry

The implementation is identical in `src/net48/BIN/DocumentTelemetryTracker.cs`
and `src/net8.0-windows/BIN/DocumentTelemetryTracker.cs`. Each `Panel` starts the
tracker and subscribes once during `OnStartup`, then unsubscribes and stops it
during `OnShutdown`.

## Architecture

`DocumentChanged` is the appropriate observer: Autodesk documents it as a
[read-only notification after commit, undo or redo](https://help.autodesk.com/cloudhelp/2017/ENU/Revit-API/files/GUID-288EF636-4EBF-4B5D-8E3F-246C810C2720.htm).
No transaction or updater is needed. It observes model changes from native
commands and add-ins; it does not identify which command or add-in originated
a change. Existing Ribbon command diagnostics continue independently.

The Revit main thread reads the counts, title, user, active view, transaction
names and categories from at most five added/modified elements combined.
It then hands a snapshot containing only strings, numbers and string lists to
a bounded dispatcher. Deleted IDs are counted but never resolved: their elements
are already unavailable. Delete-only events can therefore have empty categories.

Filtering ignores family documents, empty change sets, blank transaction names
and events whose transaction names are all exact matches for a small list of
navigation, selection or temporary-view operations. Broad substring rules and
category allowlists are deliberately avoided. Unknown/localized names remain
eligible. Modification-only events are also ignored when every changed element
was inspected and is a `View`; a partial sample cannot prove an event view-only.
This conservative filter may retain unrecognized internal operations.

One timer checks pending work every 1000 ms. Changes accumulate by document
identity, project title, user, view and commit/undo/redo operation. Documents with
the same title remain distinct. Weak document identities are used only on the
main thread and do not keep closed documents alive. Neither pending snapshots
nor worker closures contain Revit API objects.

A single `ThreadPool.QueueUserWorkItem` sender handles one batch at a time.
Monotonic timestamps enforce at least 1000 ms between dispatches. Slow requests
allow more accumulation without creating additional sender threads or delaying
Revit on network I/O. A fixed window also sends the final event of a burst;
a simple drop-on-throttle approach would lose those counts, while an indefinitely
reset trailing debounce could postpone delivery throughout continuous editing.

Pending contexts are capped at 32. Each stores at most 16 distinct transaction
names (1024 characters each) and five distinct categories (256 characters each).
Other metadata strings are capped at 1024 characters. Existing contexts can still
merge at capacity; new contexts are discarded. This is best-effort activity
telemetry, not a durable audit trail. There are no retries. Shutdown clears
pending work without waiting for the network; an already-started request may
complete. No network I/O occurs while holding the queue lock.

## Payload and transport

Payload keys match the requested contract. `details.operation` distinguishes
commit, undo and redo; `details.event_count` identifies aggregated events.
Counts are sums of change occurrences, not unique element totals across events.
Mixed batches retain all three counts; the command label uses Delete, then Add,
then Modify precedence. Transaction names and categories are bounded samples.

The sender POSTs to `https://mcp-revit-api.thuongdang531.workers.dev/` with
`BIN-Revit-Tool/1.0`, UTF-8 JSON, `DefaultWebProxy`/default proxy credentials,
and 3000 ms request and read/write timeouts. TLS 1.2 and TLS 1.3 are enabled
separately so runtimes that reject TLS 1.3 retain TLS 1.2. All transport failures
are silent; error responses are disposed.

Explicit Newtonsoft `JObject`/`JArray` tokens perform serialization. The offline
.NET 8 test exposed a missing `System.Security.Permissions` dependency when the
bundled legacy Newtonsoft DLL serialized an anonymous object. Direct JSON tokens
avoid that reflection path without replacing shared dependencies or changing
`CommandDiagnostics`.

## Verification

```powershell
dotnet build src/net48/BIN.csproj -c Release --no-restore --nologo -v minimal
dotnet build src/net8.0-windows/BIN.csproj -c Release --no-restore --nologo -v minimal
dotnet build scripts/tests/DocumentTelemetry/DocumentTelemetry.Tests.csproj -c Release --nologo -v minimal
& scripts/tests/DocumentTelemetry/bin/Release/net48/DocumentTelemetry.Tests.exe
dotnet scripts/tests/DocumentTelemetry/bin/Release/net8.0/DocumentTelemetry.Tests.dll
```

The offline harness compiles each target's actual tracker source against small
Revit test doubles. Its sender is simulated: it never sends test data to
Cloudflare. Scenarios cover noise filtering, bounded API sampling, unavailable
metadata, JSON escaping, burst counts, document/view/undo separation, bounded
memory, serialization on both runtimes, a slow/failing sender, rate limiting,
shutdown and repeated startup. API test doubles enforce main-thread access.
Production builds reference the installed Revit 2023 and Revit 2026 API DLLs.

Build and offline checks do not prove delivery in Revit. Startup subscription
changes require updating the loaded add-in/DevLoader and restarting Revit;
hot-loading a Ribbon command alone does not run `Panel.OnStartup` again.
After loading the correct DLL, verify native pipe creation, modification,
Delete, undo/redo, rapid edits and family/view filtering against received
Cloudflare payloads. Installation, a Revit restart and live endpoint delivery
are outside the completed source/build verification.
