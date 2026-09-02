# KeyGlance Developer Trial

KeyGlanceTrial is a safety-focused Windows automation prototype. A Node.js job server queues tax-field imports, a C# helper claims one job and writes it into the correct desktop window, and a WPF MockTax app stands in for commercial tax software.

## Safety guarantees

- `GET /claim` atomically claims the earliest-due queued job, preventing two helpers from receiving it.
- The helper requires an exact client, year, window title, recipient name, and UI Automation ID match before writing.
- Every written value is read back; `imported` is reported only when all values match.
- A foreground-window change stops further writes.
- A durable local ledger prevents the helper from processing the same job twice.

## Build and run

Requires Windows, Node.js 22.5+, and the .NET 8 SDK.

```powershell
# Terminal 1: job server
cd JobServer
npm start

# Terminal 2: mock tax application
dotnet run --project MockTax/MockTax.csproj -- --client "Margaret Buttle" --year 2025

# Create a job
curl.exe -X POST http://localhost:5050/jobs `
  -H "content-type: application/json" `
  -d '{"id":"job-123","client":"Margaret Buttle","year":2025,"dueDate":"2026-09-03","fields":{"Box2":"40.00","Box14":"11019.84","Box22":"1101.96"}}'

# Terminal 3: claim and import the job
dotnet run --project Helper/Helper.csproj -- --server http://localhost:5050 --client "Margaret Buttle" --year 2025
```

To test readback failure, launch MockTax with `--readonly Box22`; the helper should report `partial` and name `Box22`.

## Testing and failure handling

Run `npm test` in `JobServer/`. The automated tests cover urgency ordering, single-use and concurrent claims, valid completion, and rejection of duplicate completion. Both .NET projects build with `dotnet build`, and the matching-window happy path was exercised end to end with all three fields reported as landed.

The implementation also handles wrong client/year, multiple windows through exact-title matching, exact `Box2` versus `Box22` lookup, read-only-field mismatch, foreground-window changes, and duplicate local execution. These Windows UI paths require interactive manual testing; they are not currently covered by automated UI tests. The local ledger deliberately favors at-most-once execution: a crash after ledger recording may require manual recovery instead of retrying automatically.
