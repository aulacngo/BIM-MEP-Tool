PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS events_l1 (
  event_id TEXT PRIMARY KEY,
  occurred_at_utc TEXT NOT NULL,
  received_at_utc TEXT NOT NULL,
  schema_version TEXT NOT NULL,
  event_type TEXT NOT NULL,
  install_id_hash TEXT NOT NULL DEFAULT '',
  session_id TEXT NOT NULL DEFAULT '',
  client_sequence INTEGER,
  project_id_hash TEXT NOT NULL DEFAULT '',
  view_type TEXT NOT NULL DEFAULT 'Unknown',
  tool_id TEXT NOT NULL,
  stage TEXT NOT NULL,
  outcome TEXT NOT NULL,
  reason_code TEXT NOT NULL DEFAULT '',
  duration_ms INTEGER,
  selection_count INTEGER,
  element_signature TEXT NOT NULL DEFAULT '[]',
  context_json TEXT NOT NULL DEFAULT '{}',
  quality_json TEXT NOT NULL DEFAULT '{}',
  event_json TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_events_l1_tool_time ON events_l1(tool_id, occurred_at_utc);
CREATE INDEX IF NOT EXISTS ix_events_l1_failure_time ON events_l1(tool_id, outcome, occurred_at_utc);
CREATE INDEX IF NOT EXISTS ix_events_l1_rage ON events_l1(install_id_hash, tool_id, element_signature, occurred_at_utc);
CREATE INDEX IF NOT EXISTS ix_events_l1_project_time ON events_l1(project_id_hash, occurred_at_utc);

CREATE TABLE IF NOT EXISTS events_l2 (
  event_id TEXT PRIMARY KEY,
  occurred_at_utc TEXT NOT NULL,
  received_at_utc TEXT NOT NULL,
  schema_version TEXT NOT NULL,
  event_type TEXT NOT NULL,
  session_id TEXT NOT NULL DEFAULT '',
  client_sequence INTEGER,
  tool_id TEXT NOT NULL,
  stage TEXT NOT NULL,
  outcome TEXT NOT NULL,
  reason_code TEXT NOT NULL DEFAULT '',
  details_json TEXT NOT NULL DEFAULT '{}',
  event_json TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_events_l2_tool_time ON events_l2(tool_id, occurred_at_utc);

CREATE TABLE IF NOT EXISTS anomalies (
  anomaly_id INTEGER PRIMARY KEY AUTOINCREMENT,
  fingerprint TEXT NOT NULL UNIQUE,
  anomaly_type TEXT NOT NULL,
  tool_id TEXT NOT NULL,
  install_id_hash TEXT NOT NULL DEFAULT '',
  window_start_utc TEXT NOT NULL,
  window_end_utc TEXT NOT NULL,
  numerator INTEGER NOT NULL,
  denominator INTEGER NOT NULL,
  details_json TEXT NOT NULL DEFAULT '{}',
  detected_at_utc TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_anomalies_detected ON anomalies(detected_at_utc DESC);
CREATE INDEX IF NOT EXISTS ix_anomalies_tool ON anomalies(tool_id, detected_at_utc DESC);

CREATE TABLE IF NOT EXISTS hourly_rollups (
  hour_utc TEXT NOT NULL,
  tool_id TEXT NOT NULL,
  outcome TEXT NOT NULL,
  reason_code TEXT NOT NULL DEFAULT '',
  event_count INTEGER NOT NULL,
  failure_count INTEGER NOT NULL DEFAULT 0,
  duration_sum_ms INTEGER NOT NULL DEFAULT 0,
  duration_max_ms INTEGER,
  PRIMARY KEY (hour_utc, tool_id, outcome, reason_code)
);

CREATE INDEX IF NOT EXISTS ix_hourly_rollups_tool_time ON hourly_rollups(tool_id, hour_utc);

-- Additive V2+ migration. Safe on existing databases; no new columns required.
-- Old events without correlation remain valid; do not guess their invocation identity.
CREATE INDEX IF NOT EXISTS ix_events_l1_correlation
  ON events_l1(json_extract(event_json, '$.correlation_id')) WHERE json_valid(event_json);
CREATE INDEX IF NOT EXISTS ix_events_l2_correlation
  ON events_l2(json_extract(event_json, '$.correlation_id')) WHERE json_valid(event_json);
