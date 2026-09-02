import http from 'node:http';
import { pathToFileURL } from 'node:url';
import { openDatabase } from './db.js';
import { JobRepository, validateJob } from './jobs.js';

async function readJson(request) {
  let body = '';
  for await (const chunk of request) {
    body += chunk;
    if (body.length > 1_000_000) throw new Error('request body too large');
  }
  return JSON.parse(body || '{}');
}

function send(response, status, payload) {
  if (status === 204) {
    response.writeHead(status);
    response.end();
    return;
  }
  const body = JSON.stringify(payload);
  response.writeHead(status, { 'content-type': 'application/json', 'content-length': Buffer.byteLength(body) });
  response.end(body);
}

export function createServer({ dbPath = ':memory:' } = {}) {
  const db = openDatabase(dbPath);
  const jobs = new JobRepository(db);

  const server = http.createServer(async (request, response) => {
    try {
      const url = new URL(request.url, 'http://localhost');

      if (request.method === 'GET' && url.pathname === '/health') {
        return send(response, 200, { ok: true });
      }

      if (request.method === 'GET' && url.pathname === '/claim') {
        const job = jobs.claim();
        return job ? send(response, 200, job) : send(response, 204);
      }

      if (request.method === 'POST' && url.pathname === '/jobs') {
        const job = jobs.create(validateJob(await readJson(request)));
        return send(response, 201, job);
      }

      const resultMatch = url.pathname.match(/^\/jobs\/([^/]+)\/result$/);
      if (request.method === 'POST' && resultMatch) {
        const result = await readJson(request);
        if (!['imported', 'partial', 'stopped'].includes(result.outcome)) {
          return send(response, 400, { error: 'outcome must be imported, partial, or stopped' });
        }
        const completed = jobs.complete(decodeURIComponent(resultMatch[1]), result);
        return completed
          ? send(response, 200, completed)
          : send(response, 409, { error: 'job is missing or is not currently claimed' });
      }

      const jobMatch = url.pathname.match(/^\/jobs\/([^/]+)$/);
      if (request.method === 'GET' && jobMatch) {
        const job = jobs.get(decodeURIComponent(jobMatch[1]));
        return job ? send(response, 200, job) : send(response, 404, { error: 'job not found' });
      }

      return send(response, 404, { error: 'not found' });
    } catch (error) {
      return send(response, 400, { error: error.message });
    }
  });

  server.repository = jobs;
  server.database = db;
  return server;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const port = Number(process.env.PORT ?? 5050);
  const server = createServer({ dbPath: process.env.DB_PATH ?? './data/jobs.db' });
  server.listen(port, () => console.log(`KeyGlance JobServer listening on http://localhost:${port}`));
}

