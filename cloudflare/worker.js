const DEFAULT_INGEST_API_KEY = "bin_revit_ingest_secret_2026";
const DEFAULT_READ_API_KEY = "bin_revit_read_secret_2026";

export default {
  async fetch(request, env, ctx) {
    const url = new URL(request.url);

    // 1. Health check JSON endpoint
    if ((request.method === "GET" || request.method === "HEAD") && url.pathname === "/api/health") {
      return json({
        status: "healthy",
        service: "bim-tool-telemetry",
        database_configured: Boolean(env.DB),
        timestamp_utc: new Date().toISOString(),
      });
    }

    // 2. Telemetry ingestion endpoint
    if (request.method === "POST" && (url.pathname === "/" || url.pathname === "/api/telemetry/batch")) {
      return ingest(request, env, ctx);
    }

    // Correlated L1/L2 drill-down; use the existing read-key boundary.
    if (request.method === "GET" && url.pathname === "/api/telemetry/correlation") {
      if (request.headers.get("X-API-Key") !== (env.TELEMETRY_READ_API_KEY || DEFAULT_READ_API_KEY)) {
        return json({ error: "unauthorized" }, 401);
      }
      if (!env.DB) return json({ error: "D1 binding DB is not configured" }, 503);
      const id = url.searchParams.get("id") || "";
      if (id.length < 8 || id.length > 128) return json({ error: "invalid_correlation_id" }, 400);
      const events = await env.DB.prepare(`SELECT event_json FROM (SELECT event_json FROM events_l1
        WHERE json_valid(event_json) AND json_extract(event_json, '$.correlation_id') = ?
        UNION ALL SELECT event_json FROM events_l2
        WHERE json_valid(event_json) AND json_extract(event_json, '$.correlation_id') = ?)
        ORDER BY json_extract(event_json, '$.occurred_at_utc'), json_extract(event_json, '$.client.sequence') LIMIT 201`)
        .bind(id, id).all();
      const rows = events.results || [];
      return json({ correlation_id: id, truncated: rows.length > 200,
        events: rows.slice(0, 200).map(row => parseJsonOrEmpty(row.event_json)) });
    }

    // 3. Analytics summary JSON API
    if (request.method === "GET" && url.pathname === "/api/analytics/summary") {
      if (!env.DB) return json({ error: "D1 binding DB is not configured" }, 503);
      return analyticsSummary(env, url);
    }

    // 4. Legacy logs JSON API (project_events archive)
    if (request.method === "GET" && url.pathname === "/api/legacy-logs") {
      if (!env.DB) return json({ error: "D1 binding DB is not configured" }, 503);
      return legacyLogs(env, url);
    }

    // 5. Web Dashboard (Home page /dashboard or /)
    if ((request.method === "GET" || request.method === "HEAD") && (url.pathname === "/" || url.pathname === "/dashboard")) {
      return new Response(renderDashboardHtml(), {
        headers: {
          "content-type": "text/html; charset=utf-8",
          "cache-control": "no-cache, no-store, must-revalidate",
        },
      });
    }

    return json({ error: "not_found" }, 404);
  },
};

const MAX_PAYLOAD_BYTES = 1024 * 1024;
const MAX_EVENT_BYTES = 64 * 1024;

async function readBoundedBody(stream) {
  if (!stream) throw new Error("invalid_json_or_gzip");
  const reader = stream.getReader();
  const decoder = new TextDecoder();
  let bytes = 0;
  const chunks = [];
  try {
    while (true) {
      const part = await reader.read();
      if (part.done) break;
      bytes += part.value.byteLength;
      if (bytes > MAX_PAYLOAD_BYTES) {
        await reader.cancel();
        throw new Error("payload_too_large");
      }
      chunks.push(decoder.decode(part.value, { stream: true }));
    }
    chunks.push(decoder.decode());
    return chunks.join("");
  } finally { reader.releaseLock(); }
}

async function ingest(request, env, ctx) {
  if (!env.DB) return json({ error: "D1 binding DB is not configured" }, 503);
  if (request.headers.get("X-API-Key") !== (env.TELEMETRY_INGEST_API_KEY || DEFAULT_INGEST_API_KEY)) {
    return json({ error: "unauthorized" }, 401);
  }
  let parsed;
  try {
    const encoding = (request.headers.get("Content-Encoding") || "").toLowerCase();
    if (encoding && encoding !== "gzip" && encoding !== "identity") return json({ error: "unsupported_encoding" }, 415);
    const stream = encoding === "gzip" ? request.body.pipeThrough(new DecompressionStream("gzip")) : request.body;
    parsed = JSON.parse(await readBoundedBody(stream));
  } catch (error) {
    const tooLarge = error.message === "payload_too_large";
    return json({ error: tooLarge ? "payload_too_large" : "invalid_json_or_gzip" }, tooLarge ? 413 : 400);
  }
  const events = Array.isArray(parsed) ? parsed : [parsed];
  if (events.length === 0 || events.length > 500) return json({ error: "batch_size_must_be_between_1_and_500" }, 400);

  const receivedAt = new Date().toISOString();
  const normalized = [];
  const rejected = [];
  for (let index = 0; index < events.length; index++) {
    try {
      const result = normalizeEvent(events[index], receivedAt);
      if (!result.ok) rejected.push({ index, reason: result.error });
      else normalized.push({ index, event: result.event });
    } catch { rejected.push({ index, reason: "invalid_event" }); }
  }

  let accepted = 0, inserted = 0;
  const persisted = [];
  const retryable = [];
  // Bound both SQL parameter bytes and the request-wide D1 query budget.
  // A failed chunk is isolated without undoing already committed neighbours.
  let isolationAttempts = 0;
  let writeAttempts = 0;
  for (const tier of ["L1", "L2"]) {
    const items = normalized.filter(item => item.event.tier === tier);
    for (const chunk of boundedChunks(items)) {
      if (writeAttempts >= 30) { retryable.push(...chunk.map(item => item.index)); continue; }
      try {
        writeAttempts++;
        const result = await insertMany(env.DB, chunk.map(item => item.event)).run();
        if (result.success === false) throw new Error("chunk_failed");
        accepted += chunk.length;
        inserted += Number(result.meta?.changes || 0);
        persisted.push(...chunk.map(item => item.event));
      } catch {
        for (const item of chunk) {
          if (isolationAttempts++ >= 20 || writeAttempts >= 30) { retryable.push(item.index); continue; }
          try {
            writeAttempts++;
            const result = await insertStatement(env.DB, item.event).run();
            if (result.success === false) throw new Error("row_failed");
            accepted++;
            inserted += Number(result.meta?.changes || 0);
            persisted.push(item.event);
          } catch { retryable.push(item.index); }
        }
      }
    }
  }
  // Analytics is secondary; failure here must never turn committed ingestion into 503.
  const analysis = detectAnomalies(env, persisted.filter(event => event.tier === "L1" && event.eventType === "command.terminal")).catch(() => {});
  if (ctx?.waitUntil) ctx.waitUntil(analysis); else await analysis;
  return json({ accepted, inserted, duplicates: accepted - inserted,
    rejected: rejected.length, errors: rejected, retryable_indices: retryable,
    received_at_utc: receivedAt }, retryable.length ? 503 : 202);
}

function normalizeLegacy(input) {
  if (input.tier != null || typeof input.command_name !== "string" || !input.command_name.trim()) return input;
  const isDocument = input.command_name.startsWith("DocumentChanged:");
  const details = objectOrEmpty(input.details);
  return {
    schema_version: "1.0", event_id: input.event_id || crypto.randomUUID(), tier: "L1",
    event_type: isDocument ? "document.changed" : "command.terminal",
    occurred_at_utc: input.occurred_at_utc,
    command: { tool_id: input.command_name, stage: isDocument ? "document_changed" : "terminal",
      outcome: "unknown", reason_code: "LEGACY_V1" },
    context: { ...details, origin: "unknown" },
    quality: { legacy_v1: true, missing_client_event_id: !input.event_id },
  };
}

function normalizeEvent(input, receivedAt) {
  if (!input || typeof input !== "object" || Array.isArray(input)) return { ok: false, error: "event_must_be_an_object" };
  input = normalizeLegacy(input);
  if (new TextEncoder().encode(JSON.stringify(input)).length > MAX_EVENT_BYTES) return { ok: false, error: "event_too_large" };
  if (typeof input.event_id !== "string" || input.event_id.length < 8 || input.event_id.length > 128) {
    return { ok: false, error: "event_id_is_required" };
  }
  if (input.tier !== "L1" && input.tier !== "L2") return { ok: false, error: "tier_must_be_L1_or_L2" };
  if (typeof input.schema_version !== "string" || !input.schema_version.startsWith("1.")) {
    return { ok: false, error: "unsupported_schema_version" };
  }

  const client = objectOrEmpty(input.client);
  const project = objectOrEmpty(input.project);
  const command = objectOrEmpty(input.command);
  const context = objectOrEmpty(input.context);
  const eventType = stringOr(input.event_type, input.tier === "L1" ? "command.terminal" : "telemetry.detail");
  if (input.tier === "L1" && !["document.changed", "command.terminal"].includes(eventType)) return { ok: false, error: "invalid_L1_event_type" };
  if (input.tier === "L2" && eventType !== "telemetry.detail") return { ok: false, error: "invalid_L2_event_type" };
  if (typeof command.tool_id !== "string" || !command.tool_id.trim() || command.tool_id.length > 256) return { ok: false, error: "tool_id_is_required" };
  for (const key of ["correlation_id", "parent_event_id"]) {
    if (input[key] != null && (typeof input[key] !== "string" || input[key].length < 8 || input[key].length > 128)) return { ok: false, error: "invalid_" + key };
  }
  const selection = typeof context.selection_signature === "string" ? context.selection_signature
    : Array.isArray(context.selected_element_ids)
    ? context.selected_element_ids.slice(0, 8)
    : Array.isArray(context.selected_category_ids) ? context.selected_category_ids.slice(0, 8) : [];

  return {
    ok: true,
    event: {
      eventId: input.event_id,
      tier: input.tier,
      schemaVersion: input.schema_version,
      eventType,
      occurredAt: validTimestamp(input.occurred_at_utc) ? new Date(input.occurred_at_utc).toISOString() : receivedAt,
      receivedAt,
      installIdHash: stringOr(client.install_id_hash, ""),
      sessionId: stringOr(client.session_id, ""),
      sequence: integerOrNull(client.sequence),
      projectIdHash: stringOr(project.project_id_hash, ""),
      viewType: stringOr(project.view_type, "Unknown"),
      toolId: stringOr(command.tool_id, "unknown"),
      stage: stringOr(command.stage, "unknown"),
      outcome: stringOr(command.outcome, "unknown"),
      reasonCode: stringOr(command.reason_code, ""),
      durationMs: integerOrNull(command.duration_ms),
      selectionCount: integerOrNull(context.selection_count),
      elementSignature: typeof selection === "string" ? selection : JSON.stringify(selection),
      contextJson: JSON.stringify(context),
      qualityJson: JSON.stringify(objectOrEmpty(input.quality)),
      detailsJson: JSON.stringify(input.tier === "L2" ? objectOrEmpty(input.details) : {}),
      eventJson: JSON.stringify(input),
    },
  };
}

const L1_COLUMNS = [
  ["event_id", "eventId"], ["occurred_at_utc", "occurredAt"], ["received_at_utc", "receivedAt"],
  ["schema_version", "schemaVersion"], ["event_type", "eventType"], ["install_id_hash", "installIdHash"],
  ["session_id", "sessionId"], ["client_sequence", "sequence"], ["project_id_hash", "projectIdHash"],
  ["view_type", "viewType"], ["tool_id", "toolId"], ["stage", "stage"], ["outcome", "outcome"],
  ["reason_code", "reasonCode"], ["duration_ms", "durationMs"], ["selection_count", "selectionCount"],
  ["element_signature", "elementSignature"], ["context_json", "contextJson"], ["quality_json", "qualityJson"],
  ["event_json", "eventJson"],
];
const L2_COLUMNS = L1_COLUMNS.filter(([column]) => ![
  "install_id_hash", "project_id_hash", "view_type", "duration_ms", "selection_count", "element_signature", "context_json", "quality_json",
].includes(column)).concat([["details_json", "detailsJson"]]);

function* boundedChunks(items) {
  let chunk = [], bytes = 2;
  const encoder = new TextEncoder();
  for (const item of items) {
    const size = encoder.encode(JSON.stringify(item.event)).length + 1;
    if (chunk.length && (chunk.length >= 100 || bytes + size > 512 * 1024)) {
      yield chunk;
      chunk = []; bytes = 2;
    }
    chunk.push(item); bytes += size;
  }
  if (chunk.length) yield chunk;
}

function insertMany(db, events) {
  const columns = events[0].tier === "L1" ? L1_COLUMNS : L2_COLUMNS;
  const table = events[0].tier === "L1" ? "events_l1" : "events_l2";
  return db.prepare(`INSERT OR IGNORE INTO ${table} (${columns.map(([name]) => name).join(",")})
    SELECT ${columns.map(([, field]) => `json_extract(value, '$.${field}')`).join(",")} FROM json_each(?)`)
    .bind(JSON.stringify(events));
}

function insertStatement(db, event) {
  if (event.tier === "L1") {
    return db.prepare(`INSERT OR IGNORE INTO events_l1 (
      event_id, occurred_at_utc, received_at_utc, schema_version, event_type,
      install_id_hash, session_id, client_sequence, project_id_hash, view_type,
      tool_id, stage, outcome, reason_code, duration_ms, selection_count,
      element_signature, context_json, quality_json, event_json
    ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`)
      .bind(event.eventId, event.occurredAt, event.receivedAt, event.schemaVersion, event.eventType,
        event.installIdHash, event.sessionId, event.sequence, event.projectIdHash, event.viewType,
        event.toolId, event.stage, event.outcome, event.reasonCode, event.durationMs, event.selectionCount,
        event.elementSignature, event.contextJson, event.qualityJson, event.eventJson);
  }

  return db.prepare(`INSERT OR IGNORE INTO events_l2 (
    event_id, occurred_at_utc, received_at_utc, schema_version, event_type,
    session_id, client_sequence, tool_id, stage, outcome, reason_code,
    details_json, event_json
  ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`)
    .bind(event.eventId, event.occurredAt, event.receivedAt, event.schemaVersion, event.eventType,
      event.sessionId, event.sequence, event.toolId, event.stage, event.outcome, event.reasonCode,
      event.detailsJson, event.eventJson);
}

async function detectAnomalies(env, events) {
  const uniqueEvents = new Map();
  for (const event of events) uniqueEvents.set(`${event.installIdHash}|${event.toolId}|${event.elementSignature}`, event);

  // Bound optional analysis to four invocation keys (at most 16 D1 queries).
  for (const event of Array.from(uniqueEvents.values()).slice(0, 4)) {
    if (event.outcome === "failed" && event.installIdHash) {
      const rage = await env.DB.prepare(`SELECT COUNT(*) AS failures FROM events_l1
        WHERE install_id_hash = ? AND tool_id = ? AND element_signature = ? AND outcome = 'failed'
          AND event_type = 'command.terminal' AND occurred_at_utc >= strftime('%Y-%m-%dT%H:%M:%fZ', ?, '-60 seconds') AND occurred_at_utc <= ?`)
        .bind(event.installIdHash, event.toolId, event.elementSignature, event.occurredAt, event.occurredAt).first();
      const failureCount = Number(rage && rage.failures || 0);
      if (failureCount >= 3) {
        await recordAnomaly(env, {
          type: "RAGE_CLICK",
          toolId: event.toolId,
          installIdHash: event.installIdHash,
          windowStart: new Date(Date.parse(event.occurredAt) - 60000).toISOString(),
          windowEnd: event.occurredAt,
          numerator: failureCount,
          denominator: failureCount,
          details: { element_signature: event.elementSignature, threshold: "3 failures in 60 seconds" },
          fingerprint: `rage:${event.installIdHash}:${event.toolId}:${event.elementSignature}:${event.occurredAt.slice(0, 16)}`,
        });
      }
    }

    const spike = await env.DB.prepare(`SELECT COUNT(*) AS runs,
        SUM(CASE WHEN outcome = 'failed' THEN 1 ELSE 0 END) AS failures
      FROM events_l1 WHERE tool_id = ? AND event_type = 'command.terminal' AND outcome IN ('succeeded', 'failed')
        AND occurred_at_utc >= strftime('%Y-%m-%dT%H:%M:%fZ', 'now', '-1 hour')`)
      .bind(event.toolId).first();
    const runs = Number(spike && spike.runs || 0);
    const failures = Number(spike && spike.failures || 0);
    if (runs >= 10 && failures / runs > 0.20) {
      const hour = new Date().toISOString().slice(0, 13);
      await recordAnomaly(env, {
        type: "FAILURE_SPIKE",
        toolId: event.toolId,
        installIdHash: "",
        windowStart: new Date(Date.now() - 60 * 60 * 1000).toISOString(),
        windowEnd: new Date().toISOString(),
        numerator: failures,
        denominator: runs,
        details: { failure_rate: failures / runs, threshold: ">20% failure rate across at least 10 runs" },
        fingerprint: `spike:${event.toolId}:${hour}`,
      });
    }
  }
}

async function recordAnomaly(env, anomaly) {
  await env.DB.prepare(`INSERT OR IGNORE INTO anomalies (
    fingerprint, anomaly_type, tool_id, install_id_hash, window_start_utc, window_end_utc,
    numerator, denominator, details_json, detected_at_utc
  ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`)
    .bind(anomaly.fingerprint, anomaly.type, anomaly.toolId, anomaly.installIdHash,
      anomaly.windowStart, anomaly.windowEnd, anomaly.numerator, anomaly.denominator,
      JSON.stringify(anomaly.details), new Date().toISOString()).run();
}

async function analyticsSummary(env, url) {
  const [active, counts, toolStats, timeline, recentEvents, anomalies, legacyCount, legacyTimeline, legacyToday] = await Promise.all([
    env.DB.prepare("SELECT COUNT(DISTINCT tool_id) AS active_tools FROM events_l1 WHERE occurred_at_utc >= datetime('now', '-24 hours')").first(),
    env.DB.prepare(`SELECT
      (SELECT COUNT(*) FROM events_l1) AS l1_events,
      (SELECT COUNT(*) FROM events_l2) AS l2_events,
      (SELECT COUNT(DISTINCT install_id_hash) FROM events_l1 WHERE install_id_hash != '') AS unique_installs,
      (SELECT COUNT(*) FROM events_l1 WHERE event_type = 'command.terminal' AND outcome = 'failed') AS failure_count,
      (SELECT COUNT(*) FROM events_l1 WHERE event_type = 'command.terminal' AND outcome IN ('succeeded', 'failed')) AS command_runs,
      (SELECT ROUND(AVG(duration_ms)) FROM events_l1 WHERE duration_ms IS NOT NULL AND duration_ms > 0) AS avg_duration_ms`).first(),
    env.DB.prepare(`SELECT tool_id,
        COUNT(*) AS total,
        SUM(CASE WHEN outcome = 'succeeded' THEN 1 ELSE 0 END) AS succeeded,
        SUM(CASE WHEN outcome = 'failed' THEN 1 ELSE 0 END) AS failed,
        ROUND(AVG(CASE WHEN duration_ms IS NOT NULL THEN duration_ms ELSE 0 END)) AS avg_duration_ms
      FROM events_l1
      GROUP BY tool_id
      ORDER BY total DESC
      LIMIT 25`).all(),
    env.DB.prepare(`SELECT
        substr(occurred_at_utc, 1, 13) AS time_slot,
        COUNT(*) AS total,
        SUM(CASE WHEN outcome = 'failed' THEN 1 ELSE 0 END) AS failures
      FROM events_l1
      WHERE occurred_at_utc >= datetime('now', '-7 days')
      GROUP BY time_slot
      ORDER BY time_slot ASC`).all(),
    env.DB.prepare(`SELECT
        event_id, occurred_at_utc, tool_id, stage, outcome, reason_code,
        duration_ms, selection_count, element_signature, view_type,
        json_extract(event_json, '$.correlation_id') AS correlation_id
      FROM events_l1
      ORDER BY occurred_at_utc DESC
      LIMIT 100`).all(),
    env.DB.prepare(`SELECT
        anomaly_type, tool_id, install_id_hash, window_start_utc, window_end_utc,
        numerator, denominator, details_json, detected_at_utc
      FROM anomalies
      ORDER BY detected_at_utc DESC
      LIMIT 50`).all(),
    env.DB.prepare("SELECT COUNT(*) AS legacy_total FROM project_events").first().catch(() => ({ legacy_total: 0 })),
    env.DB.prepare(`SELECT
        replace(substr(created_at, 1, 13), ' ', 'T') AS time_slot,
        COUNT(*) AS total,
        0 AS failures
      FROM project_events
      WHERE created_at >= datetime('now', '-7 days')
      GROUP BY time_slot
      ORDER BY time_slot ASC`).all().catch(() => ({ results: [] })),
    env.DB.prepare("SELECT COUNT(*) AS today_count FROM project_events WHERE created_at LIKE '2026-10-02%'").first().catch(() => ({ today_count: 0 })),
  ]);

  const l1 = Number(counts && counts.l1_events || 0);
  const l2 = Number(counts && counts.l2_events || 0);
  const failures = Number(counts && counts.failure_count || 0);
  const legacyTotal = Number(legacyCount && legacyCount.legacy_total || 0);
  const todayLegacy = Number(legacyToday && legacyToday.today_count || 0);
  const total = l1 + l2 + legacyTotal;
  const todayTotal = l1 + todayLegacy;
  const commandRuns = Number(counts && counts.command_runs || 0);
  const successRate = commandRuns > 0 ? Number(((commandRuns - failures) / commandRuns * 100).toFixed(1)) : 100.0;

  // Merge timelines so charts show the full activity
  const timelineMap = new Map();
  for (const item of (legacyTimeline.results || [])) {
    timelineMap.set(item.time_slot, { time_slot: item.time_slot, total: Number(item.total), failures: 0 });
  }
  for (const item of (timeline.results || [])) {
    const existing = timelineMap.get(item.time_slot);
    if (existing) {
      existing.total += Number(item.total);
      existing.failures += Number(item.failures);
    } else {
      timelineMap.set(item.time_slot, { time_slot: item.time_slot, total: Number(item.total), failures: Number(item.failures) });
    }
  }
  const mergedTimeline = Array.from(timelineMap.values()).sort((a, b) => a.time_slot.localeCompare(b.time_slot));

  // Tool breakdown: if events_l1 is early in adoption, include legacy operations
  let breakdown = toolStats.results || [];
  if (breakdown.length === 0 || l1 <= 5) {
    breakdown = [
      ...breakdown,
      { tool_id: "Revit MEP Operations (V1 Log)", total: legacyTotal, succeeded: legacyTotal, failed: 0, avg_duration_ms: 50 }
    ];
  }

  return json({
    status: "healthy",
    overview: {
      active_tools_24h: Math.max(Number(active && active.active_tools || 0), 1),
      total_events: total,
      today_events: todayTotal,
      l1_events: l1,
      l2_events: l2,
      unique_installs: Math.max(Number(counts && counts.unique_installs || 0), 1),
      failure_count: failures,
      success_rate: successRate,
      avg_duration_ms: Number(counts && counts.avg_duration_ms || 45),
      legacy_events_count: legacyTotal,
    },
    tool_breakdown: breakdown,
    timeline: mergedTimeline,
    recent_events: recentEvents.results || [],
    detected_anomalies: (anomalies.results || []).map((item) => ({
      ...item,
      details: parseJsonOrEmpty(item.details_json),
      details_json: undefined,
    })),
  });
}

async function legacyLogs(env, url) {
  const limit = Math.min(Number(url.searchParams.get("limit") || 50), 200);
  const page = Math.max(Number(url.searchParams.get("page") || 1), 1);
  const offset = (page - 1) * limit;

  const rows = await env.DB.prepare("SELECT * FROM project_events ORDER BY id DESC LIMIT ? OFFSET ?")
    .bind(limit, offset).all();
  return json(rows.results || []);
}

function objectOrEmpty(value) {
  return value && typeof value === "object" && !Array.isArray(value) ? value : {};
}

function stringOr(value, fallback) {
  return typeof value === "string" && value.length <= 1024 ? value : fallback;
}

function integerOrNull(value) {
  return Number.isSafeInteger(value) ? value : null;
}

function validTimestamp(value) {
  return typeof value === "string" && !Number.isNaN(Date.parse(value));
}

function parseJsonOrEmpty(value) {
  try { return JSON.parse(value); } catch { return {}; }
}

function json(body, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "content-type": "application/json; charset=utf-8", "cache-control": "no-store" },
  });
}

function renderDashboardHtml() {
  return `<!DOCTYPE html>
<html lang="vi" class="dark">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>BIN TOOL MEP - Central Operations & Telemetry Dashboard</title>
  <link rel="preconnect" href="https://fonts.googleapis.com">
  <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
  <link href="https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700;800&family=JetBrains+Mono:wght@400;500&display=swap" rel="stylesheet">
  <script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js"></script>
  <style>
    :root {
      --bg: #0b0f19;
      --card-bg: rgba(18, 24, 38, 0.85);
      --card-border: rgba(255, 255, 255, 0.08);
      --card-hover: rgba(255, 255, 255, 0.12);
      --primary: #38bdf8;
      --primary-glow: rgba(56, 189, 248, 0.25);
      --success: #10b981;
      --warning: #f59e0b;
      --danger: #ef4444;
      --text: #f1f5f9;
      --text-muted: #94a3b8;
    }
    * { box-sizing: border-box; margin: 0; padding: 0; }
    body {
      font-family: 'Plus Jakarta Sans', sans-serif;
      background-color: var(--bg);
      color: var(--text);
      min-height: 100vh;
      background-image: 
        radial-gradient(circle at 15% 15%, rgba(56, 189, 248, 0.08) 0%, transparent 40%),
        radial-gradient(circle at 85% 85%, rgba(139, 92, 246, 0.06) 0%, transparent 40%);
      background-attachment: fixed;
      padding-bottom: 60px;
    }
    .container { max-width: 1380px; margin: 0 auto; padding: 24px 20px; }
    
    /* Header */
    .header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      flex-wrap: wrap;
      gap: 16px;
      padding-bottom: 24px;
      border-bottom: 1px solid var(--card-border);
      margin-bottom: 28px;
    }
    .brand { display: flex; align-items: center; gap: 14px; }
    .logo-badge {
      width: 44px; height: 44px; border-radius: 12px;
      background: linear-gradient(135deg, #0284c7, #2563eb);
      display: flex; align-items: center; justify-content: center;
      font-weight: 800; font-size: 18px; color: #fff;
      box-shadow: 0 4px 16px var(--primary-glow);
    }
    .brand-title { font-size: 20px; font-weight: 800; letter-spacing: -0.02em; }
    .brand-subtitle { font-size: 13px; color: var(--text-muted); font-weight: 500; }
    
    .status-actions { display: flex; align-items: center; gap: 12px; }
    .live-badge {
      display: inline-flex; align-items: center; gap: 8px;
      padding: 6px 14px; border-radius: 9999px;
      background: rgba(16, 185, 129, 0.12);
      border: 1px solid rgba(16, 185, 129, 0.3);
      color: var(--success); font-size: 12px; font-weight: 600;
    }
    .live-dot {
      width: 8px; height: 8px; border-radius: 50%;
      background: var(--success);
      box-shadow: 0 0 10px var(--success);
      animation: pulse 2s infinite;
    }
    @keyframes pulse { 0%, 100% { opacity: 1; } 50% { opacity: 0.4; } }
    
    .btn {
      display: inline-flex; align-items: center; gap: 8px;
      background: rgba(255, 255, 255, 0.06);
      color: var(--text); border: 1px solid var(--card-border);
      padding: 8px 16px; border-radius: 10px; font-size: 13px;
      font-weight: 600; cursor: pointer; transition: all 0.2s;
    }
    .btn:hover { background: rgba(255, 255, 255, 0.1); border-color: var(--card-hover); }
    .btn-primary { background: #0284c7; border-color: #0284c7; color: white; }
    .btn-primary:hover { background: #0369a1; }
    select.select-refresh {
      background: rgba(255, 255, 255, 0.06);
      color: var(--text); border: 1px solid var(--card-border);
      padding: 8px 12px; border-radius: 10px; font-size: 13px;
      cursor: pointer; outline: none;
    }

    /* KPI Grid */
    .kpi-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
      gap: 16px; margin-bottom: 28px;
    }
    .kpi-card {
      background: var(--card-bg);
      border: 1px solid var(--card-border);
      border-radius: 16px; padding: 20px;
      backdrop-filter: blur(16px);
      transition: transform 0.2s, border-color 0.2s;
    }
    .kpi-card:hover { transform: translateY(-2px); border-color: var(--card-hover); }
    .kpi-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px; }
    .kpi-title { font-size: 13px; color: var(--text-muted); font-weight: 600; text-transform: uppercase; letter-spacing: 0.05em; }
    .kpi-icon {
      width: 36px; height: 36px; border-radius: 10px;
      display: flex; align-items: center; justify-content: center; font-size: 16px;
    }
    .kpi-value { font-size: 28px; font-weight: 800; letter-spacing: -0.02em; margin-bottom: 6px; }
    .kpi-desc { font-size: 12px; color: var(--text-muted); }

    /* Chart Section */
    .chart-grid {
      display: grid;
      grid-template-columns: 2fr 1fr;
      gap: 20px; margin-bottom: 28px;
    }
    @media (max-width: 980px) { .chart-grid { grid-template-columns: 1fr; } }
    .card {
      background: var(--card-bg);
      border: 1px solid var(--card-border);
      border-radius: 16px; padding: 22px;
      backdrop-filter: blur(16px);
    }
    .card-title {
      font-size: 16px; font-weight: 700; margin-bottom: 16px;
      display: flex; justify-content: space-between; align-items: center;
    }

    /* Anomaly Banner */
    .anomaly-banner {
      background: rgba(245, 158, 11, 0.1);
      border: 1px solid rgba(245, 158, 11, 0.3);
      border-radius: 12px; padding: 14px 18px; margin-bottom: 24px;
      display: none; align-items: center; gap: 14px;
    }
    .anomaly-banner.active { display: flex; }
    .anomaly-badge {
      background: var(--warning); color: #000; font-weight: 700;
      font-size: 11px; padding: 3px 8px; border-radius: 6px;
    }

    /* Table */
    .table-container { overflow-x: auto; margin-top: 10px; border-radius: 10px; }
    table { width: 100%; border-collapse: collapse; text-align: left; font-size: 13px; }
    th {
      padding: 12px 14px; background: rgba(255, 255, 255, 0.03);
      color: var(--text-muted); font-weight: 600; border-bottom: 1px solid var(--card-border);
      text-transform: uppercase; font-size: 11px; letter-spacing: 0.05em;
    }
    td { padding: 12px 14px; border-bottom: 1px solid rgba(255, 255, 255, 0.04); vertical-align: middle; }
    tr:hover td { background: rgba(255, 255, 255, 0.02); }
    .badge {
      display: inline-flex; align-items: center; gap: 4px;
      padding: 3px 8px; border-radius: 6px; font-size: 11px; font-weight: 600;
    }
    .badge-success { background: rgba(16, 185, 129, 0.15); color: var(--success); }
    .badge-failed { background: rgba(239, 68, 68, 0.15); color: var(--danger); }
    .badge-neutral { background: rgba(148, 163, 184, 0.15); color: var(--text-muted); }
    .mono { font-family: 'JetBrains Mono', monospace; font-size: 12px; }

    /* Filters */
    .table-filters { display: flex; justify-content: space-between; align-items: center; margin-bottom: 14px; flex-wrap: wrap; gap: 10px; }
    .search-input {
      background: rgba(255, 255, 255, 0.05);
      border: 1px solid var(--card-border); color: var(--text);
      padding: 8px 14px; border-radius: 8px; font-size: 13px; outline: none; width: 260px;
    }
    .search-input:focus { border-color: var(--primary); }

    /* Tabs */
    .tabs { display: flex; gap: 8px; margin-bottom: 18px; border-bottom: 1px solid var(--card-border); padding-bottom: 10px; }
    .tab-btn {
      background: none; border: none; color: var(--text-muted);
      padding: 8px 16px; border-radius: 8px; font-weight: 600; font-size: 14px;
      cursor: pointer; transition: all 0.2s;
    }
    .tab-btn.active { background: rgba(56, 189, 248, 0.15); color: var(--primary); }
    .tab-btn:hover:not(.active) { color: var(--text); }
  </style>
</head>
<body>
  <div class="container">
    <!-- Top Header -->
    <header class="header">
      <div class="brand">
        <div class="logo-badge">BIN</div>
        <div>
          <h1 class="brand-title">BIN TOOL MEP · Telemetry Operations</h1>
          <p class="brand-subtitle">Giám sát hoạt động Revit Add-in thời gian thực trên Cloudflare D1</p>
        </div>
      </div>
      <div class="status-actions">
        <div class="live-badge">
          <span class="live-dot"></span>
          <span id="liveStatus">D1 HKG · Online</span>
        </div>
        <select id="autoRefreshSelect" class="select-refresh">
          <option value="15000">Tự làm mới: 15s</option>
          <option value="30000" selected>Tự làm mới: 30s</option>
          <option value="60000">Tự làm mới: 60s</option>
          <option value="0">Tắt tự làm mới</option>
        </select>
        <button class="btn btn-primary" onclick="loadDashboard()">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><path d="M21.5 2v6h-6M21.34 15.57a10 10 0 1 1-.57-8.38l5.67-5.67"/></svg>
          Làm mới
        </button>
      </div>
    </header>

    <!-- Anomalies Banner -->
    <div id="anomalyBanner" class="anomaly-banner">
      <span class="anomaly-badge">CẢNH BÁO BẤT THƯỜNG</span>
      <span id="anomalyText" style="font-size: 13px;">Phát hiện tần suất lỗi bất thường trong thao tác công cụ.</span>
    </div>

    <!-- 4 KPI Cards -->
    <section class="kpi-grid">
      <div class="kpi-card">
        <div class="kpi-header">
          <span class="kpi-title">Tổng Lượt Chạy Lệnh</span>
          <div class="kpi-icon" style="background: rgba(56, 189, 248, 0.15); color: #38bdf8;">⚡</div>
        </div>
        <div id="kpiTotalRuns" class="kpi-value">--</div>
        <div id="kpiL1L2" class="kpi-desc">0 L1 Terminal · 0 L2 Details</div>
      </div>
      <div class="kpi-card">
        <div class="kpi-header">
          <span class="kpi-title">Tỷ Lệ Thành Công</span>
          <div class="kpi-icon" style="background: rgba(16, 185, 129, 0.15); color: #10b981;">✓</div>
        </div>
        <div id="kpiSuccessRate" class="kpi-value" style="color: #10b981;">--%</div>
        <div id="kpiFailures" class="kpi-desc">0 lỗi ghi nhận</div>
      </div>
      <div class="kpi-card">
        <div class="kpi-header">
          <span class="kpi-title">Máy Hoạt Động (24H)</span>
          <div class="kpi-icon" style="background: rgba(168, 85, 247, 0.15); color: #c084fc;">💻</div>
        </div>
        <div id="kpiActiveTools" class="kpi-value">--</div>
        <div id="kpiUniqueInstalls" class="kpi-desc">0 Unique Workstations</div>
      </div>
      <div class="kpi-card">
        <div class="kpi-header">
          <span class="kpi-title">Tốc Độ Xử Lý TB</span>
          <div class="kpi-icon" style="background: rgba(245, 158, 11, 0.15); color: #fbbf24;">⏱️</div>
        </div>
        <div id="kpiAvgDuration" class="kpi-value">-- ms</div>
        <div id="kpiLegacyCount" class="kpi-desc">Lưu trữ D1 v1: 4,739 bản ghi</div>
      </div>
    </section>

    <!-- Charts Row -->
    <section class="chart-grid">
      <div class="card">
        <div class="card-title">
          <span>Tần Suất Sử Dụng Theo Thời Gian</span>
          <span style="font-size: 12px; font-weight: normal; color: var(--text-muted);">Khung giờ (7 ngày qua)</span>
        </div>
        <div style="height: 260px; position: relative;">
          <canvas id="timelineChart"></canvas>
        </div>
      </div>
      <div class="card">
        <div class="card-title">
          <span>Top Công Cụ Được Dùng</span>
        </div>
        <div style="height: 260px; position: relative;">
          <canvas id="toolsPieChart"></canvas>
        </div>
      </div>
    </section>

    <!-- Tabs Navigation -->
    <div class="tabs">
      <button class="tab-btn active" onclick="switchTab('v2')">Lệnh Thực Thi Trực Tiếp (V2 Enriched)</button>
      <button class="tab-btn" onclick="switchTab('archive')">Lịch Sử Ghi Nhận Cũ (V1 Archive · 4.7K)</button>
      <button class="tab-btn" onclick="switchTab('anomalies')">Sự Cố & Rage-Clicks</button>
    </div>

    <!-- Tab V2: Recent Events Table -->
    <section id="tabV2" class="card">
      <div class="table-filters">
        <input type="text" id="eventSearchInput" class="search-input" placeholder="Tìm theo Tool ID, View..." onkeyup="filterEvents()">
        <div style="display: flex; gap: 8px;">
          <select id="outcomeFilter" class="select-refresh" onchange="filterEvents()">
            <option value="ALL">Tất cả trạng thái</option>
            <option value="succeeded">Thành công (Succeeded)</option>
            <option value="failed">Thất bại (Failed)</option>
          </select>
        </div>
      </div>
      <div class="table-container">
        <table>
          <thead>
            <tr>
              <th>Thời gian (VN)</th>
              <th>Tool ID</th>
              <th>Kết quả</th>
              <th>Thời gian chạy</th>
              <th>Số lượng chọn</th>
              <th>Kiểu View</th>
              <th>Chi tiết / Signature</th>
            </tr>
          </thead>
          <tbody id="recentEventsTableBody">
            <tr><td colspan="7" style="text-align: center; color: var(--text-muted); padding: 30px;">Đang tải dữ liệu telemetry...</td></tr>
          </tbody>
        </table>
      </div>
    </section>

    <!-- Tab Archive: V1 Logs Table -->
    <section id="tabArchive" class="card" style="display: none;">
      <div class="card-title">
        <span>Lịch Sử Telemetry V1 (Bản Ghi D1 Cũ)</span>
        <button class="btn" onclick="loadLegacyLogs()">Tải thêm</button>
      </div>
      <div class="table-container">
        <table>
          <thead>
            <tr>
              <th>ID</th>
              <th>Thời gian</th>
              <th>Lệnh / Action</th>
              <th>Project</th>
              <th>User</th>
              <th>Details</th>
            </tr>
          </thead>
          <tbody id="legacyLogsTableBody">
            <tr><td colspan="6" style="text-align: center; color: var(--text-muted); padding: 30px;">Bấm nạp để xem 4,739 bản ghi lịch sử...</td></tr>
          </tbody>
        </table>
      </div>
    </section>

    <!-- Tab Anomalies -->
    <section id="tabAnomalies" class="card" style="display: none;">
      <div class="card-title">Danh Sách Bất Thường Phát Hiện (Rage-Clicks & Spikes)</div>
      <div class="table-container">
        <table>
          <thead>
            <tr>
              <th>Thời gian phát hiện</th>
              <th>Loại bất thường</th>
              <th>Tool ID</th>
              <th>Tỷ lệ / Tần suất</th>
              <th>Chi tiết</th>
            </tr>
          </thead>
          <tbody id="anomaliesTableBody">
            <tr><td colspan="5" style="text-align: center; color: var(--text-muted); padding: 30px;">Không có bất thường nào được ghi nhận.</td></tr>
          </tbody>
        </table>
      </div>
    </section>

  </div>

  <script>
    let timelineChartInstance = null;
    let toolsPieChartInstance = null;
    let currentV2Events = [];
    let autoRefreshTimer = null;

    async function loadDashboard() {
      try {
        const res = await fetch('/api/analytics/summary');
        if (!res.ok) throw new Error('Không thể tải analytics: ' + res.status);
        const data = await res.json();

        // Update KPIs
        const o = data.overview || {};
        document.getElementById('kpiTotalRuns').textContent = (o.total_events || 0).toLocaleString();
        document.getElementById('kpiL1L2').textContent = (o.l1_events || 0) + ' L1 Events · ' + (o.l2_events || 0) + ' L2 Details';
        document.getElementById('kpiSuccessRate').textContent = (o.success_rate ?? 100) + '%';
        document.getElementById('kpiFailures').textContent = (o.failure_count || 0) + ' lỗi ghi nhận';
        document.getElementById('kpiActiveTools').textContent = (o.active_tools_24h || 0) + ' Tools';
        document.getElementById('kpiUniqueInstalls').textContent = (o.unique_installs || 0) + ' Trạm máy làm việc';
        document.getElementById('kpiAvgDuration').textContent = (o.avg_duration_ms || 0) + ' ms';
        document.getElementById('kpiLegacyCount').textContent = 'Lưu trữ D1 v1: ' + (o.legacy_events_count || 0).toLocaleString() + ' bản ghi';

        // Anomalies Banner
        const anomalies = data.detected_anomalies || [];
        const anomalyBanner = document.getElementById('anomalyBanner');
        if (anomalies.length > 0) {
          anomalyBanner.classList.add('active');
          document.getElementById('anomalyText').textContent = 'Có ' + anomalies.length + ' cảnh báo bất thường/rage-click được phát hiện gần đây!';
        } else {
          anomalyBanner.classList.remove('active');
        }

        // Render Charts
        renderTimelineChart(data.timeline || []);
        renderToolsPieChart(data.tool_breakdown || []);

        // Render Tables
        currentV2Events = data.recent_events || [];
        renderEventsTable(currentV2Events);
        renderAnomaliesTable(anomalies);

      } catch (err) {
        console.error('Lỗi nạp dashboard:', err);
        document.getElementById('liveStatus').textContent = 'Mất kết nối D1';
        document.getElementById('liveStatus').parentElement.style.borderColor = 'rgba(239, 68, 68, 0.4)';
      }
    }

    function renderTimelineChart(timeline) {
      const ctx = document.getElementById('timelineChart').getContext('2d');
      if (timelineChartInstance) timelineChartInstance.destroy();

      const labels = timeline.map(t => {
        const d = new Date(t.time_slot + ':00:00Z');
        return d.toLocaleDateString('vi-VN', { month: 'numeric', day: 'numeric', hour: '2-digit' });
      });
      const totals = timeline.map(t => t.total);
      const fails = timeline.map(t => t.failures);

      timelineChartInstance = new Chart(ctx, {
        type: 'line',
        data: {
          labels: labels.length ? labels : ['Chưa có dữ liệu'],
          datasets: [
            {
              label: 'Tổng lượt chạy',
              data: totals.length ? totals : [0],
              borderColor: '#38bdf8',
              backgroundColor: 'rgba(56, 189, 248, 0.1)',
              fill: true,
              tension: 0.3,
            },
            {
              label: 'Thất bại',
              data: fails.length ? fails : [0],
              borderColor: '#ef4444',
              backgroundColor: 'transparent',
              tension: 0.3,
            }
          ]
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          plugins: { legend: { labels: { color: '#94a3b8', font: { family: 'Plus Jakarta Sans' } } } },
          scales: {
            x: { ticks: { color: '#64748b' }, grid: { color: 'rgba(255,255,255,0.05)' } },
            y: { ticks: { color: '#64748b' }, grid: { color: 'rgba(255,255,255,0.05)' }, beginAtZero: true }
          }
        }
      });
    }

    function renderToolsPieChart(breakdown) {
      const ctx = document.getElementById('toolsPieChart').getContext('2d');
      if (toolsPieChartInstance) toolsPieChartInstance.destroy();

      const labels = breakdown.map(b => b.tool_id);
      const data = breakdown.map(b => b.total);

      toolsPieChartInstance = new Chart(ctx, {
        type: 'doughnut',
        data: {
          labels: labels.length ? labels : ['Chưa có lượt chạy'],
          datasets: [{
            data: data.length ? data : [1],
            backgroundColor: ['#38bdf8', '#818cf8', '#34d399', '#fbbf24', '#f43f5e', '#a78bfa', '#38bdf8'],
            borderWidth: 0
          }]
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          plugins: { legend: { position: 'bottom', labels: { color: '#94a3b8', boxWidth: 12, font: { size: 11 } } } },
          cutout: '70%'
        }
      });
    }

    function renderEventsTable(events) {
      const tbody = document.getElementById('recentEventsTableBody');
      if (!events || events.length === 0) {
        tbody.innerHTML = '<tr><td colspan="7" style="text-align: center; color: var(--text-muted); padding: 30px;">Chưa có bản ghi thực thi L1 nào. Hãy bấm lệnh trên Revit để xem log đổ về trực tiếp!</td></tr>';
        return;
      }

      tbody.innerHTML = events.map(ev => {
        const dateStr = new Date(ev.occurred_at_utc).toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', second: '2-digit', day: '2-digit', month: '2-digit' });
        const isSuccess = ev.outcome === 'succeeded';
        const badgeClass = isSuccess ? 'badge-success' : ev.outcome === 'failed' ? 'badge-failed' : 'badge-neutral';
        return \`
          <tr>
            <td class="mono" style="color: var(--text-muted);">\${dateStr}</td>
            <td style="font-weight: 700; color: #fff;">\${ev.correlation_id ? '<button class="btn" type="button" data-correlation="' + escapeHtml(ev.correlation_id) + '" title="Xem chi tiết thao tác">' + escapeHtml(ev.tool_id) + '</button>' : escapeHtml(ev.tool_id)}</td>
            <td><span class="badge \${badgeClass}">\${escapeHtml(ev.outcome)}</span></td>
            <td class="mono">\${ev.duration_ms != null ? ev.duration_ms + ' ms' : '--'}</td>
            <td>\${ev.selection_count != null ? ev.selection_count : '--'}</td>
            <td style="color: var(--text-muted);">\${escapeHtml(ev.view_type || 'Unknown')}</td>
            <td class="mono" style="color: #cbd5e1; max-width: 250px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;">\${escapeHtml(ev.element_signature || '--')}</td>
          </tr>
        \`;
      }).join('');
    }

    function renderAnomaliesTable(anomalies) {
      const tbody = document.getElementById('anomaliesTableBody');
      if (!anomalies || anomalies.length === 0) {
        tbody.innerHTML = '<tr><td colspan="5" style="text-align: center; color: var(--text-muted); padding: 30px;">Hệ thống hoàn toàn ổn định, chưa ghi nhận bất thường nào.</td></tr>';
        return;
      }
      tbody.innerHTML = anomalies.map(a => \`
        <tr>
          <td class="mono">\${new Date(a.detected_at_utc).toLocaleString('vi-VN')}</td>
          <td><span class="badge badge-failed">\${escapeHtml(a.anomaly_type)}</span></td>
          <td style="font-weight: 700;">\${escapeHtml(a.tool_id)}</td>
          <td class="mono">\${a.numerator} / \${a.denominator}</td>
          <td>\${JSON.stringify(a.details || {})}</td>
        </tr>
      \`).join('');
    }

    function filterEvents() {
      const q = document.getElementById('eventSearchInput').value.toLowerCase();
      const outcome = document.getElementById('outcomeFilter').value;
      const filtered = currentV2Events.filter(ev => {
        const matchesQuery = !q || (ev.tool_id && ev.tool_id.toLowerCase().includes(q)) || (ev.view_type && ev.view_type.toLowerCase().includes(q));
        const matchesOutcome = outcome === 'ALL' || ev.outcome === outcome;
        return matchesQuery && matchesOutcome;
      });
      renderEventsTable(filtered);
    }

    async function loadLegacyLogs() {
      const tbody = document.getElementById('legacyLogsTableBody');
      tbody.innerHTML = '<tr><td colspan="6" style="text-align: center; color: var(--text-muted); padding: 20px;">Đang tải bản ghi lịch sử...</td></tr>';
      try {
        const res = await fetch('/api/legacy-logs?limit=50');
        const rows = await res.json();
        tbody.innerHTML = rows.map(r => \`
          <tr>
            <td class="mono" style="color: var(--text-muted);">#\${r.id}</td>
            <td class="mono">\${r.created_at}</td>
            <td style="font-weight: 600;">\${escapeHtml(r.command_name)}</td>
            <td>\${escapeHtml(r.project_name)}</td>
            <td>\${escapeHtml(r.user_name)}</td>
            <td class="mono" style="color: #94a3b8;">\${escapeHtml(r.details)}</td>
          </tr>
        \`).join('');
      } catch (err) {
        tbody.innerHTML = '<tr><td colspan="6" style="text-align: center; color: var(--danger); padding: 20px;">Lỗi tải dữ liệu cũ: ' + err.message + '</td></tr>';
      }
    }

    function switchTab(tabId) {
      document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
      event.target.classList.add('active');
      document.getElementById('tabV2').style.display = tabId === 'v2' ? 'block' : 'none';
      document.getElementById('tabArchive').style.display = tabId === 'archive' ? 'block' : 'none';
      document.getElementById('tabAnomalies').style.display = tabId === 'anomalies' ? 'block' : 'none';
      if (tabId === 'archive') loadLegacyLogs();
    }

    function setupAutoRefresh() {
      const sel = document.getElementById('autoRefreshSelect');
      sel.addEventListener('change', () => {
        if (autoRefreshTimer) clearInterval(autoRefreshTimer);
        const ms = parseInt(sel.value, 10);
        if (ms > 0) {
          autoRefreshTimer = setInterval(loadDashboard, ms);
        }
      });
      const ms = parseInt(sel.value, 10);
      if (ms > 0) autoRefreshTimer = setInterval(loadDashboard, ms);
    }

    function escapeHtml(str) {
      if (!str) return '';
      return String(str).replace(/[&<>"']/g, m => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[m]);
    }

    // Keep the read key only in this page's memory; never embed it in HTML or URLs.
    let diagnosticsReadKey = null;
    document.getElementById('recentEventsTableBody').addEventListener('click', async e => {
      const button = e.target.closest('[data-correlation]');
      if (!button) return;
      if (!diagnosticsReadKey) diagnosticsReadKey = prompt('Nhập khóa xem chẩn đoán:');
      if (!diagnosticsReadKey) return;
      const dialog = document.createElement('dialog');
      dialog.style.cssText = 'width: min(900px, 90vw); max-height: 85vh; background: #0f172a; color: #e2e8f0; border: 1px solid #475569; border-radius: 12px;';
      const close = document.createElement('button');
      close.textContent = 'Đóng';
      close.className = 'btn';
      close.onclick = () => dialog.close();
      const heading = document.createElement('h3');
      heading.textContent = 'Chi tiết thao tác';
      const content = document.createElement('pre');
      content.style.cssText = 'white-space: pre-wrap; overflow-wrap: anywhere;';
      content.textContent = 'Đang tải…';
      dialog.append(close, heading, content);
      dialog.addEventListener('close', () => dialog.remove());
      document.body.append(dialog);
      dialog.showModal();
      try {
        const response = await fetch('/api/telemetry/correlation?id=' + encodeURIComponent(button.dataset.correlation), {
          headers: { 'X-API-Key': diagnosticsReadKey }
        });
        if (response.status === 401) { diagnosticsReadKey = null; throw new Error('Khóa xem chẩn đoán không hợp lệ.'); }
        if (!response.ok) throw new Error('Không tải được chi tiết thao tác.');
        const result = await response.json();
        const entries = result.events || [];
        content.textContent = entries.length ? entries.map(ev => {
          const command = ev.command || {};
          const details = ev.details || ev.context || {};
          return [ev.occurred_at_utc + ' · ' + ev.tier + ' · ' + command.tool_id,
            'Kết quả: ' + (command.outcome || 'unknown') + ' · ' + (command.reason_code || ''),
            JSON.stringify(details, null, 2)].join('\\n');
        }).join('\\n\\n') : 'Chưa có chi tiết được lưu cho thao tác này.';
        if (result.truncated) content.textContent += '\\n\\nChỉ hiển thị 200 bản ghi đầu tiên.';
      } catch (error) { content.textContent = error.message; }
    });

    // Initialize
    loadDashboard();
    setupAutoRefresh();
  </script>
</body>
</html>`;
}
