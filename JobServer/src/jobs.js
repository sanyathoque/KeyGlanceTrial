function parseJob(row) {
  if (!row) return null;
  return {
    id: row.id,
    client: row.client,
    year: row.year,
    dueDate: row.due_date,
    fields: JSON.parse(row.fields_json),
    status: row.status,
    claimedAt: row.claimed_at,
    completedAt: row.completed_at,
    result: row.result_json ? JSON.parse(row.result_json) : null,
    createdAt: row.created_at
  };
}

export class JobRepository {
  constructor(db) {
    this.db = db;
  }

  create(input) {
    const now = new Date().toISOString();
    this.db.prepare(`
      INSERT INTO import_jobs (id, client, year, due_date, fields_json, created_at)
      VALUES (?, ?, ?, ?, ?, ?)
    `).run(input.id, input.client, input.year, input.dueDate, JSON.stringify(input.fields), now);
    return this.get(input.id);
  }

  get(id) {
    return parseJob(this.db.prepare('SELECT * FROM import_jobs WHERE id = ?').get(id));
  }

  // The selection and state transition intentionally happen in one SQL statement.
  claim() {
    const claimedAt = new Date().toISOString();
    const row = this.db.prepare(`
      UPDATE import_jobs
      SET status = 'claimed', claimed_at = ?
      WHERE id = (
        SELECT id FROM import_jobs
        WHERE status = 'queued'
        ORDER BY due_date ASC, created_at ASC
        LIMIT 1
      )
      RETURNING *
    `).get(claimedAt);
    return parseJob(row);
  }

  complete(id, result) {
    const completedAt = new Date().toISOString();
    const row = this.db.prepare(`
      UPDATE import_jobs
      SET status = 'completed', completed_at = ?, result_json = ?
      WHERE id = ? AND status = 'claimed'
      RETURNING *
    `).get(completedAt, JSON.stringify(result), id);
    return parseJob(row);
  }
}

export function validateJob(input) {
  if (!input || typeof input !== 'object') throw new Error('JSON body must be an object');
  for (const field of ['id', 'client', 'dueDate']) {
    if (typeof input[field] !== 'string' || input[field].trim() === '') {
      throw new Error(`${field} is required`);
    }
  }
  if (!Number.isInteger(input.year)) throw new Error('year must be an integer');
  if (!/^\d{4}-\d{2}-\d{2}$/.test(input.dueDate) ||
      new Date(`${input.dueDate}T00:00:00Z`).toISOString().slice(0, 10) !== input.dueDate) {
    throw new Error('dueDate must be a valid date in YYYY-MM-DD format');
  }
  if (!input.fields || typeof input.fields !== 'object' || Array.isArray(input.fields)) {
    throw new Error('fields must be an object');
  }
  if (Object.values(input.fields).some(value => typeof value !== 'string')) {
    throw new Error('field values must be strings');
  }
  return input;
}

