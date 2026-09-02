# KeyGlance Developer Trial

This is a standalone implementation for the KeyGlance automation exercise.

The original RAGRecruit repositories are not copied, modified, or required by this project. The trial uses a small Node job server, a Windows WPF mock tax application, and a Windows C# helper.

## Design

```text
JobServer (Node + SQLite)
  GET  /claim                 atomic queued -> claimed transition
  POST /jobs/:id/result       claimed -> completed transition
             |
             v
Helper (.NET / Windows UI Automation)
  exact window identity -> exact AutomationId -> write -> readback -> result
             |
             v
MockTax (WPF)
```

The important safety properties are:

- `/claim` is a single SQLite `UPDATE ... RETURNING` statement ordered by due date.
- The helper verifies the exact client and year before any field is changed.
- Fields are found only by exact UI Automation IDs.
- Every write is read back and compared before it counts as landed.
- Foreground-window changes cancel the run.
- The helper records a job ID before the first UI mutation, giving at-most-once behavior after restarts.

## Run the job server

Requires Node.js 22.5 or later because the server uses the built-in `node:sqlite` module.

```sh
cd JobServer
npm start
```

The server listens on `http://localhost:5050` by default. Set `PORT` or `DB_PATH` to override those values.

Create a job:

```sh
curl -X POST http://localhost:5050/jobs \
  -H 'content-type: application/json' \
  -d '{"id":"job-123","client":"Margaret Buttle","year":2025,"dueDate":"2026-09-03","fields":{"Box2":"40.00","Box14":"11019.84","Box22":"1101.96"}}'
```

## Windows projects

Open `MockTax/MockTax.csproj` and `Helper/Helper.csproj` on Windows with the .NET 8 SDK. Start MockTax with a title such as `MockTax - Margaret Buttle 2025`; then run the helper with the server URL and the matching client/year.

```text
Helper.exe --server http://localhost:5050 --client "Margaret Buttle" --year 2025
```

`MockTax` supports `--readonly Box22` to exercise the write/readback failure path.

## Test coverage

From `JobServer/`:

```sh
npm test
```

The tests cover due-date ordering, single-use claims, simultaneous claims, valid completion, and rejection of a second completion.

## Conversation context

This section records the complete project-relevant context and decisions from the conversation that led to this repository.

### 1. RAGRecruit Legal review

The initial request was to access and analyze the RAGRecruit Legal frontend and server. The review identified:

- The frontend is a React 18/Create React App application with recruiting, outreach, and legal routes.
- The legal UI is concentrated in a very large `LegalPanel.js`, while legal API calls are centralized in `src/utils/legal-api.js`.
- The Node/Express server combines recruiting, legal AI, outreach, conversations, billing/authentication, and integrations.
- Legal routes use Firebase authentication and integration API keys, with tenant-scoped persistence queries based on `userId`.
- The legal subsystem contains specialized agents and models for matters, documents, folders, playbooks, firm profiles, workflows, and activity.
- Strengths included explicit tenant scoping, a firm-profile context model, broad legal workflow coverage, and escaped model-generated memo HTML.
- The highest-priority review items were splitting `LegalPanel.js`, reducing server workload coupling, tightening the global 100 MB body limit, restricting open CORS, improving API-key lifecycle controls, reducing verbose authentication logging, replacing global console suppression with structured logging, and aligning frontend/backend API contracts.
- The review was architectural and targeted rather than a line-by-line audit, and no RAGRecruit files were modified.

### 2. KeyGlance Developer Trial assessment

The conversation then analyzed the KeyGlance developer trial assignment. The assignment tests a small safety-critical automation system rather than an AI/RAG system.

The three core invariants are:

1. Never use the wrong client window. A client/name or year mismatch must cause zero UI mutations.
2. Never report success based only on sent keystrokes. The helper must read the UI back and verify the exact value.
3. Never execute a job twice. The server claim and the local helper must both prevent duplicate execution.

The required behaviors discussed were:

- Claim the most urgent job first using deterministic `dueDate ASC` ordering.
- Implement `/claim` as an atomic state transition rather than find-then-save.
- Verify exact MockTax window identity using the client and year.
- Find `Box2`, `Box14`, and `Box22` by exact AutomationId, so `Box2` cannot match `Box22`.
- Exercise a read-only `Box22` and classify a readback mismatch as partial/failed.
- Persist a local processed-job ledger.
- Stop when the foreground window changes.
- Support multiple MockTax windows without choosing an arbitrary first match.

The nine test scenarios mapped to happy path, wrong client, wrong year, exact field lookup, read-only readback failure, duplicate execution, urgency ordering, concurrent claims, and multiple windows.

### 3. Architecture decisions

The conversation explicitly decided:

- Do not copy or modify the original RAGRecruit Legal server.
- Create a separate KeyGlance project/repository.
- Reuse only general engineering patterns as inspiration, such as route separation and persistence structure.
- Do not reuse RAGRecruit legal AI, RAG, Astra DB/vector search, Firebase, Stripe, Anthropic/OpenAI integrations, DMS, Gmail, matters, or the React legal panel.
- Do not use a vector database for this trial. The assignment starts after document extraction and supplies structured fields; it requires transactional queue semantics, atomic claims, durable state, and exactly-once/at-most-once protections.
- Do not adopt Uber/YouTube-scale system design. A small reliability-focused design is sufficient: JobServer, transactional database, C# helper, and MockTax.

A vector database may belong upstream in a production tax-document extraction system, but it is not the system of record for this trial's job queue.

### 4. Implementation completed

A standalone project was created with:

- `JobServer/`: Node.js HTTP server using SQLite and the built-in `node:sqlite` module.
- `Helper/`: .NET 8 Windows console helper using UI Automation.
- `MockTax/`: .NET 8 WPF mock tax application.

The job server implements:

- `GET /health`
- `POST /jobs`
- `GET /claim`
- `GET /jobs/:id`
- `POST /jobs/:id/result`

Its claim operation updates the earliest queued job to claimed in one SQL statement. Its result operation only transitions a claimed job to completed, rejecting a second completion.

The helper implements:

- Exact client/year matching.
- Exact MockTax window-title matching.
- Exact AutomationId field lookup.
- Foreground-window checks before and after UI mutations.
- Write, readback, and comparison.
- Partial/stopped/imported result reporting.
- A durable JSON processed-job ledger marked before the first UI mutation.

### 5. Verification and GitHub publication

The .NET 8 SDK was downloaded locally under the workspace so the original RAGRecruit repositories and system installation would remain unaffected.

Verification completed successfully:

- Node job-server tests: 3 passing.
- Helper project: .NET build succeeded with 0 errors and 0 warnings.
- MockTax project: .NET build succeeded with 0 errors and 0 warnings.
- Job-server health check returned `{"ok":true}`.

A public GitHub repository was created and published:

- Repository: https://github.com/sanyathoque/KeyGlanceTrial
- Branch: `main`
- The final repository structure contains `JobServer/`, `Helper/`, `MockTax/`, `.gitignore`, and this `README.md`.
- Build artifacts and local database/runtime data are excluded by `.gitignore`.
- The original RAGRecruit Legal repositories remain untouched.

### Privacy note

This is a project-context record, not a verbatim export of private messages or links. Private email URLs, authentication details, credentials, and unrelated personal information are intentionally omitted.