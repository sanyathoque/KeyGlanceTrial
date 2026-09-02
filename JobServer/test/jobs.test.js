import test from 'node:test';
import assert from 'node:assert/strict';
import { createServer } from '../src/server.js';

async function runningServer() {
  const server = createServer();
  await new Promise(resolve => server.listen(0, resolve));
  const address = server.address();
  return { server, base: `http://127.0.0.1:${address.port}` };
}

async function close(server) {
  server.database.close();
  await new Promise(resolve => server.close(resolve));
}

async function createJob(base, job) {
  const response = await fetch(`${base}/jobs`, {
    method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(job)
  });
  assert.equal(response.status, 201);
  return response.json();
}

const job = (id, dueDate) => ({
  id, client: 'Margaret Buttle', year: 2025, dueDate,
  fields: { Box2: '40.00', Box14: '11019.84', Box22: '1101.96' }
});

test('claims the earliest due job and does not claim it twice', async () => {
  const { server, base } = await runningServer();
  try {
    await createJob(base, job('later', '2026-09-04'));
    await createJob(base, job('urgent', '2026-09-02'));
    const first = await fetch(`${base}/claim`);
    const second = await fetch(`${base}/claim`);
    assert.equal((await first.json()).id, 'urgent');
    assert.equal((await second.json()).id, 'later');
    assert.equal((await fetch(`${base}/claim`)).status, 204);
  } finally { await close(server); }
});

test('simultaneous claims return different jobs', async () => {
  const { server, base } = await runningServer();
  try {
    await createJob(base, job('one', '2026-09-02'));
    await createJob(base, job('two', '2026-09-03'));
    const responses = await Promise.all(Array.from({ length: 2 }, () => fetch(`${base}/claim`)));
    const ids = (await Promise.all(responses.map(response => response.json()))).map(value => value.id);
    assert.deepEqual(new Set(ids), new Set(['one', 'two']));
  } finally { await close(server); }
});

test('a claimed job can be completed only once', async () => {
  const { server, base } = await runningServer();
  try {
    await createJob(base, job('complete-me', '2026-09-02'));
    const claimed = await (await fetch(`${base}/claim`)).json();
    const result = { outcome: 'imported', landedFields: ['Box2', 'Box14', 'Box22'], failedFields: [], reason: '' };
    const completed = await fetch(`${base}/jobs/${claimed.id}/result`, {
      method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(result)
    });
    const duplicate = await fetch(`${base}/jobs/${claimed.id}/result`, {
      method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(result)
    });
    assert.equal(completed.status, 200);
    assert.equal(duplicate.status, 409);
  } finally { await close(server); }
});

