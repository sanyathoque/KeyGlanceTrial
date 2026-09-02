import fs from 'node:fs';
import path from 'node:path';
import { DatabaseSync } from 'node:sqlite';

export function openDatabase(dbPath = process.env.DB_PATH ?? './data/jobs.db') {
  if (dbPath !== ':memory:') {
    fs.mkdirSync(path.dirname(path.resolve(dbPath)), { recursive: true });
  }

  const db = new DatabaseSync(dbPath);
  db.exec(`
    PRAGMA journal_mode = WAL;
    PRAGMA foreign_keys = ON;
    CREATE TABLE IF NOT EXISTS import_jobs (
      id TEXT PRIMARY KEY,
      client TEXT NOT NULL,
      year INTEGER NOT NULL,
      due_date TEXT NOT NULL,
      fields_json TEXT NOT NULL,
      status TEXT NOT NULL CHECK (status IN ('queued', 'claimed', 'completed')) DEFAULT 'queued',
      claimed_at TEXT,
      completed_at TEXT,
      result_json TEXT,
      created_at TEXT NOT NULL
    );
    CREATE INDEX IF NOT EXISTS idx_import_jobs_claim_order
      ON import_jobs(status, due_date, created_at);
  `);
  return db;
}

