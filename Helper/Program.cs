using System.Net.Http;
using System.Windows.Automation;

namespace KeyGlance.Helper;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var serverUrl = ValueAfter(args, "--server") ?? "http://localhost:5050";
        var expectedClient = ValueAfter(args, "--client");
        var pollMilliseconds = int.TryParse(ValueAfter(args, "--poll-ms"), out var configuredPollMilliseconds)
            ? configuredPollMilliseconds
            : 1_000;
        if (expectedClient is null ||
            !int.TryParse(ValueAfter(args, "--year"), out var expectedYear) ||
            pollMilliseconds < 100)
        {
            Console.Error.WriteLine("Usage: Helper --server URL --client CLIENT --year YEAR [--ledger PATH] [--poll-ms 1000] [--once]");
            return 2;
        }

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        using var http = new HttpClient { BaseAddress = new Uri(serverUrl) };
        var client = new JobClient(http);
        var store = new ProcessedJobStore(ValueAfter(args, "--ledger") ?? "processed-jobs.json");
        var once = args.Any(value => string.Equals(value, "--once", StringComparison.OrdinalIgnoreCase));
        Console.WriteLine($"Polling {serverUrl} every {pollMilliseconds} ms. Press Ctrl+C to stop.");

        while (!cancellation.IsCancellationRequested)
        {
            ImportJob? job;
            try
            {
                job = await client.ClaimAsync(cancellation.Token);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine($"Claim failed: {error.Message}");
                if (once) return 1;
                await DelayBeforeNextPoll(pollMilliseconds, cancellation.Token);
                continue;
            }

            if (job is null)
            {
                if (once) return 0;
                await DelayBeforeNextPoll(pollMilliseconds, cancellation.Token);
                continue;
            }

            Console.WriteLine($"Claimed {job.Id} for {job.Client} {job.Year}.");
            int result;
            try
            {
                result = await ProcessJob(client, store, job, expectedClient, expectedYear);
            }
            catch (Exception error)
            {
                Console.Error.WriteLine($"Processing {job.Id} failed unexpectedly: {error.Message}");
                result = 1;
            }
            if (once) return result;
        }

        Console.WriteLine("Polling stopped.");
        return 0;
    }

    private static async Task<int> ProcessJob(
        JobClient client,
        ProcessedJobStore store,
        ImportJob job,
        string expectedClient,
        int expectedYear)
    {
        var landed = new List<string>();
        var failed = new List<string>();
        var reasons = new List<string>();
        try
        {
            if (!string.Equals(job.Client, expectedClient, StringComparison.Ordinal) || job.Year != expectedYear)
                throw new InvalidOperationException("Claimed job does not match the helper's requested client/year.");

            var finder = new WindowFinder();
            var window = finder.FindExact(job.Client, job.Year);

            var recipient = WindowFinder.FindExactField(window, "RecipientName");
            var actualRecipient = ((ValuePattern)recipient.GetCurrentPattern(ValuePattern.Pattern)).Current.Value;
            if (!string.Equals(actualRecipient, job.Client, StringComparison.Ordinal))
                throw new InvalidOperationException("RecipientName does not exactly match the claimed client.");

            if (!store.TryMarkBeforeMutation(job.Id))
            {
                await Report(client, job.Id, "stopped", [], [], "Job was already processed by this helper.");
                Console.WriteLine($"Stopped {job.Id}: already processed by this helper.");
                return 0;
            }

            var foreground = new ForegroundGuard(window);
            var writer = new TaxFieldWriter(foreground);
            foreach (var field in job.Fields)
            {
                foreground.EnsureTargetIsForeground();
                try
                {
                    var actual = writer.WriteAndRead(window, field.Key, field.Value);
                    if (string.Equals(actual, field.Value, StringComparison.Ordinal)) landed.Add(field.Key);
                    else
                    {
                        failed.Add(field.Key);
                        reasons.Add($"{field.Key}: readback differed from expected value");
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception error)
                {
                    failed.Add(field.Key);
                    reasons.Add($"{field.Key}: {error.Message}");
                }
            }

            var outcome = failed.Count == 0 ? "imported" : "partial";
            await Report(client, job.Id, outcome, landed, failed, string.Join("; ", reasons));
            Console.WriteLine($"Reported {job.Id} as {outcome}; landed: {FormatFields(landed)}; failed: {FormatFields(failed)}.");
            return failed.Count == 0 ? 0 : 1;
        }
        catch (OperationCanceledException error)
        {
            await Report(client, job.Id, "stopped", landed, failed, error.Message);
            Console.WriteLine($"Stopped {job.Id}: {error.Message}");
            return 1;
        }
        catch (Exception error)
        {
            var outcome = landed.Count == 0 ? "stopped" : "partial";
            await Report(client, job.Id, outcome, landed, failed, error.Message);
            Console.WriteLine($"Reported {job.Id} as {outcome}: {error.Message}");
            return 1;
        }
    }

    private static async Task DelayBeforeNextPoll(int milliseconds, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(milliseconds, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static Task Report(JobClient client, string id, string outcome, List<string> landed, List<string> failed, string reason) =>
        client.CompleteAsync(id, new JobResult { Outcome = outcome, LandedFields = landed, FailedFields = failed, Reason = reason }, CancellationToken.None);

    private static string FormatFields(List<string> fields) => fields.Count == 0 ? "none" : string.Join(", ", fields);

    private static string? ValueAfter(string[] args, string option)
    {
        var index = Array.FindIndex(args, value => string.Equals(value, option, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
