import { test } from 'node:test';
import assert from 'node:assert/strict';
import { DatabaseSync } from 'node:sqlite';
import { readFileSync } from 'node:fs';
import { gzipSync } from 'node:zlib';
import { Script, createContext } from 'node:vm';
import worker from '../../cloudflare/worker.js';

const schema = readFileSync(new URL('../../cloudflare/schema.sql', import.meta.url), 'utf8');
class D1 {
  constructor() {
    this.db = new DatabaseSync(':memory:');
    this.db.exec(schema);
    this.db.exec(schema); // Additive migration must be repeatable.
    this.calls = 0;
  }
  prepare(sql) {
    let args = [];
    const db = this;
    return {
      bind(...values) { args = values; return this; },
      async run() {
        db.calls++;
        assert.ok(args.every(v => typeof v !== 'string' || Buffer.byteLength(v) < 2_000_000), 'D1 bound string limit');
        if (db.unavailable || args.some(v => String(v).includes('poison-id'))) throw Error('injected persistence failure');
        const result = db.db.prepare(sql).run(...args);
        return { success: true, meta: { changes: result.changes } };
      },
      async first() {
        db.calls++;
        if (db.failAnalytics) throw Error('analytics unavailable');
        return db.db.prepare(sql).get(...args);
      },
      async all() { db.calls++; return { results: db.db.prepare(sql).all(...args) }; },
    };
  }
  count(table) { return this.db.prepare(`SELECT COUNT(*) AS n FROM ${table}`).get().n; }
}
const event = (id, extra = {}) => ({
  event_id: id, schema_version: '1.0', tier: 'L1', event_type: 'command.terminal',
  occurred_at_utc: '2026-10-03T03:00:00.000Z',
  command: { tool_id: 'mep.move_connect', stage: 'terminal', outcome: 'succeeded' },
  ...extra,
});
async function ingest(db, body, options = {}) {
  const pending = [];
  const bytes = typeof body === 'string' ? body : JSON.stringify(body);
  const response = await worker.fetch(new Request('https://test/api/telemetry/batch', {
    method: 'POST', headers: { 'X-API-Key': 'test-key', ...(options.gzip ? { 'Content-Encoding': 'gzip' } : {}) },
    body: options.gzip ? gzipSync(bytes) : bytes,
  }), { DB: db, TELEMETRY_INGEST_API_KEY: 'test-key' }, { waitUntil(p) { pending.push(p); } });
  await Promise.all(pending);
  return { status: response.status, body: await response.json() };
}

test('mixed V1, document L1, L2 and malformed neighbours persist independently', async () => {
  const db = new D1();
  const correlation = 'invocation-00001';
  const result = await ingest(db, [
    { command_name: 'DocumentChanged: Add', details: { categories: ['Pipes'], added_count: 3 } },
    event('doc-00001', { event_type: 'document.changed', correlation_id: correlation,
      command: { tool_id: 'DocumentChanged:TransactionCommitted', outcome: 'observed' }, context: { added_count: 2 } }),
    null, { ...event('bad-00001'), tier: 'V1' },
    event('detail-00001', { tier: 'L2', event_type: 'telemetry.detail', correlation_id: correlation,
      parent_event_id: 'terminal-00001', details: { stack_trace: 'Revit.Callsite', exception_type: 'System.Exception' } }),
  ], { gzip: true });
  assert.equal(result.status, 202);
  assert.equal(result.body.accepted, 3);
  assert.equal(result.body.inserted, 3);
  assert.deepEqual(result.body.errors.map(x => x.index), [2, 3]);
  assert.equal(db.count('events_l1'), 2);
  assert.equal(db.count('events_l2'), 1);
  assert.equal(db.db.prepare('SELECT added_count FROM (SELECT json_extract(context_json,\'$.added_count\') added_count FROM events_l1 WHERE event_id = ?)').get('doc-00001').added_count, 2);
  const request = new Request(`https://test/api/telemetry/correlation?id=${correlation}`, { headers: { 'X-API-Key': 'read-key' } });
  const trace = await worker.fetch(request, { DB: db, TELEMETRY_READ_API_KEY: 'read-key' });
  const records = await trace.json();
  assert.equal(trace.status, 200);
  assert.equal(records.events.length, 2);
  assert.ok(records.events.some(x => x.details?.stack_trace === 'Revit.Callsite'));
  const plan = db.db.prepare("EXPLAIN QUERY PLAN SELECT event_json FROM events_l1 WHERE json_valid(event_json) AND json_extract(event_json, '$.correlation_id') = ?").all(correlation);
  assert.ok(plan.some(x => x.detail.includes('ix_events_l1_correlation')));
});

test('idempotent retries and event-level errors return explicit counts', async () => {
  const db = new D1();
  const input = [event('terminal-00001'), event('terminal-00001')];
  const first = await ingest(db, input);
  assert.equal(first.body.inserted, 1);
  assert.equal(first.body.duplicates, 1);
  const second = await ingest(db, input);
  assert.equal(second.body.inserted, 0);
  assert.equal(second.body.duplicates, 2);
  const invalid = await ingest(db, [{ tier: 'L1' }, false]);
  assert.equal(invalid.status, 202);
  assert.equal(invalid.body.accepted, 0);
  assert.equal(invalid.body.rejected, 2);
});

test('isolate a failed SQL chunk; analytics failure never reverses receipt', async () => {
  const db = new D1();
  db.failAnalytics = true;
  const result = await ingest(db, [event('valid-00001'), event('poison-id'), event('valid-00002')]);
  assert.equal(result.status, 503);
  assert.equal(result.body.accepted, 2);
  assert.deepEqual(result.body.retryable_indices, [1]);
  assert.equal(db.count('events_l1'), 2);
  const receipt = await ingest(db, event('valid-00003'));
  assert.equal(receipt.status, 202);
  assert.equal(receipt.body.accepted, 1);
});

test('500 events remain within the D1 query budget, including total outage', async () => {
  const db = new D1();
  const result = await ingest(db, Array.from({ length: 500 }, (_, i) => event(`event-${i.toString().padStart(5, '0')}`)));
  assert.equal(result.body.accepted, 500);
  assert.equal(db.count('events_l1'), 500);
  assert.ok(db.calls <= 50, `D1 calls ${db.calls}`);
  const offline = new D1();
  offline.unavailable = true;
  const failed = await ingest(offline, Array.from({ length: 500 }, (_, i) => event(`offline-${i}`)));
  assert.equal(failed.status, 503);
  assert.equal(failed.body.retryable_indices.length, 500);
  assert.ok(offline.calls <= 30);
});

test('wire errors, decompressed limits, event size and authentication', async () => {
  const db = new D1();
  assert.equal((await ingest(db, '{bad json')).status, 400);
  assert.equal((await ingest(db, ' '.repeat(1024 * 1024 + 1), { gzip: true })).status, 413);
  const oversized = await ingest(db, [event('big-00001', { context: { text: 'x'.repeat(65536) } }), event('small-00001')]);
  assert.equal(oversized.body.accepted, 1);
  assert.equal(oversized.body.rejected, 1);
  const unauthorized = await worker.fetch(new Request('https://test/api/telemetry/batch', { method: 'POST', body: '{}' }), { DB: db });
  assert.equal(unauthorized.status, 401);
  const read = await worker.fetch(new Request('https://test/api/telemetry/correlation?id=example-0001'), { DB: db });
  assert.equal(read.status, 401);
});

test('large escaped details stay below the D1 parameter limit without losing valid rows', async () => {
  const db = new D1();
  const input = Array.from({ length: 12 }, (_, i) => event('large-' + i.toString().padStart(5, '0'), {
    tier: 'L2', event_type: 'telemetry.detail', details: { text: '"'.repeat(30000) },
  }));
  const result = await ingest(db, input, { gzip: true });
  assert.equal(result.status, 202);
  assert.equal(result.body.accepted, 12);
  assert.equal(db.count('events_l2'), 12);
  assert.ok(db.calls <= 30);
});

test('DocumentChanged does not enter command anomaly denominators', async () => {
  const db = new D1();
  const result = await ingest(db, event('document-0001', { event_type: 'document.changed', command: { tool_id: 'DocumentChanged:TransactionUndone', outcome: 'observed' } }));
  assert.equal(result.status, 202);
  assert.equal(db.calls, 1);
  assert.equal(db.count('anomalies'), 0);
});

test('dashboard drill-down script parses and safely renders correlation buttons', async () => {
  const response = await worker.fetch(new Request('https://test/dashboard'), {});
  const html = await response.text();
  const script = [...html.matchAll(/<script>([\s\S]*?)<\/script>/g)].at(-1)[1];
  new Script(script); // Check the rendered browser script, including nested template escapes.
  const tbody = { innerHTML: '', addEventListener() {} };
  const sandbox = createContext({ document: { getElementById() { return tbody; } } });
  new Script(script.replace(/\/\/ Initialize\s+loadDashboard\(\);\s+setupAutoRefresh\(\);/, '')).runInContext(sandbox);
  sandbox.renderEventsTable([{ occurred_at_utc: new Date().toISOString(), tool_id: '<unsafe>', correlation_id: 'id-"unsafe', outcome: 'failed' }]);
  assert.match(tbody.innerHTML, /data-correlation="id-&quot;unsafe"/);
  assert.match(tbody.innerHTML, /&lt;unsafe&gt;/);
  assert.doesNotMatch(tbody.innerHTML, /<unsafe>/);
});

if (process.env.TELEMETRY_FIXTURE) {
  test('actual C# client envelopes ingest and correlate through SQLite', async () => {
    const db = new D1();
    const input = JSON.parse(readFileSync(process.env.TELEMETRY_FIXTURE, 'utf8').replace(/^\uFEFF/, ''));
    const result = await ingest(db, input, { gzip: true });
    assert.equal(result.status, 202);
    assert.equal(result.body.rejected, 0);
    assert.equal(result.body.accepted, input.length);
    assert.ok(db.count('events_l1') > 0 && db.count('events_l2') > 0);
  });
}
