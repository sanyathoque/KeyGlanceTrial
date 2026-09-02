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

