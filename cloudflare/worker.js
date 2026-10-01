const DEFAULT_INGEST_API_KEY = "bin_revit_ingest_secret_2026";

export default {
  async fetch(request, env) {
    const url = new URL(request.url);

    if (request.method === "GET" && (url.pathname === "/" || url.pathname === "/api/health")) {
      return json({
        status: "healthy",
        service: "bim-tool-telemetry",
        database_configured: Boolean(env.DB),
        timestamp_utc: new Date().toISOString(),
      });
    }

    if (request.method === "GET" && url.pathname === "/api/analytics/summary") {
      if (!env.DB) return json({ error: "D1 binding DB is not configured" }, 503);
      return analyticsSummary(env);
    }

    if (request.method === "POST" && (url.pathname === "/" || url.pathname === "/api/telemetry/batch")) {
      return ingest(request, env);
    }

    return json({ error: "not_found" }, 404);
  },
};

async function ingest(request, env) {
  if (!env.DB) return json({ error: "D1 binding DB is not configured" }, 503);

  const expectedApiKey = env.TELEMETRY_INGEST_API_KEY || DEFAULT_INGEST_API_KEY;
  if (request.headers.get("X-API-Key") !== expectedApiKey) {
    return json({ error: "unauthorized" }, 401);
  }

  let parsed;
  try {
    const contentEncoding = (request.headers.get("Content-Encoding") || "").toLowerCase();
    const body = contentEncoding === "gzip"
      ? request.body.pipeThrough(new DecompressionStream("gzip"))
      : request.body;
    const payload = await new Response(body).text();
    if (payload.length > 1024 * 1024) return json({ error: "payload_too_large" }, 413);
    parsed = JSON.parse(payload);
  } catch {
    return json({ error: "invalid_json_or_gzip" }, 400);
  }

  const events = Array.isArray(parsed) ? parsed : [parsed];
  if (events.length === 0 || events.length > 500) {
    return json({ error: "batch_size_must_be_between_1_and_500" }, 400);
  }

  const receivedAt = new Date().toISOString();
  const normalized = [];
  for (const event of events) {
    const result = normalizeEvent(event, receivedAt);
    if (!result.ok) return json({ error: "invalid_event", detail: result.error }, 400);
    normalized.push(result.event);
  }

  try {
    await env.DB.batch(normalized.map((event) => insertStatement(env.DB, event)));
    await detectAnomalies(env, normalized.filter((event) => event.tier === "L1"));
  } catch (error) {
    // Do not echo event content or credentials in an ingest error response.
    return json({ error: "persistence_failed", detail: String(error && error.message || "unknown") }, 503);
  }

  return json({ accepted: normalized.length, received_at_utc: receivedAt }, 202);
}

function normalizeEvent(input, receivedAt) {
  if (!input || typeof input !== "object" || Array.isArray(input)) return { ok: false, error: "event_must_be_an_object" };
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
      eventType: stringOr(input.event_type, input.tier === "L1" ? "command.terminal" : "telemetry.detail"),
      occurredAt: validTimestamp(input.occurred_at_utc) ? input.occurred_at_utc : receivedAt,
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

  for (const event of uniqueEvents.values()) {
    if (event.outcome === "failed" && event.installIdHash) {
      const rage = await env.DB.prepare(`SELECT COUNT(*) AS failures FROM events_l1
        WHERE install_id_hash = ? AND tool_id = ? AND element_signature = ? AND outcome = 'failed'
          AND occurred_at_utc >= datetime(?, '-60 seconds')`)
        .bind(event.installIdHash, event.toolId, event.elementSignature, event.occurredAt).first();
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
      FROM events_l1 WHERE tool_id = ? AND outcome IN ('succeeded', 'failed')
        AND occurred_at_utc >= datetime('now', '-1 hour')`)
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

async function analyticsSummary(env) {
  const [active, counts, anomalies] = await Promise.all([
    env.DB.prepare("SELECT COUNT(DISTINCT tool_id) AS active_tools FROM events_l1 WHERE occurred_at_utc >= datetime('now', '-24 hours')").first(),
    env.DB.prepare(`SELECT
      (SELECT COUNT(*) FROM events_l1) AS l1_events,
      (SELECT COUNT(*) FROM events_l2) AS l2_events,
      (SELECT COUNT(*) FROM events_l1 WHERE outcome = 'failed') AS failure_count`).first(),
    env.DB.prepare(`SELECT anomaly_type, tool_id, install_id_hash, window_start_utc, window_end_utc,
      numerator, denominator, details_json, detected_at_utc
      FROM anomalies ORDER BY detected_at_utc DESC LIMIT 100`).all(),
  ]);
  return json({
    active_tools: Number(active && active.active_tools || 0),
    event_counts: {
      l1: Number(counts && counts.l1_events || 0),
      l2: Number(counts && counts.l2_events || 0),
      total: Number(counts && counts.l1_events || 0) + Number(counts && counts.l2_events || 0),
    },
    failure_count: Number(counts && counts.failure_count || 0),
    detected_anomalies: (anomalies.results || []).map((item) => ({
      ...item,
      details: parseJsonOrEmpty(item.details_json),
      details_json: undefined,
    })),
  });
}

function objectOrEmpty(value) {
  return value && typeof value === "object" && !Array.isArray(value) ? value : {};
}

function stringOr(value, fallback) {
  return typeof value === "string" && value.length <= 1024 ? value : fallback;
}

function integerOrNull(value) {
  return Number.isInteger(value) ? value : null;
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
