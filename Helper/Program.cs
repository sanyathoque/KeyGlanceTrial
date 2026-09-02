using System.Net.Http;
using System.Windows.Automation;

namespace KeyGlance.Helper;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var serverUrl = ValueAfter(args, "--server") ?? "http://localhost:5050";
        var expectedClient = ValueAfter(args, "--client");
        if (expectedClient is null || !int.TryParse(ValueAfter(args, "--year"), out var expectedYear))
        {
            Console.Error.WriteLine("Usage: Helper --server URL --client CLIENT --year YEAR [--ledger PATH]");
            return 2;
        }

        using var http = new HttpClient { BaseAddress = new Uri(serverUrl) };
        var client = new JobClient(http);
        var job = await client.ClaimAsync(CancellationToken.None);
        if (job is null) return 0;

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

            var ledgerPath = ValueAfter(args, "--ledger") ?? "processed-jobs.json";
            var store = new ProcessedJobStore(ledgerPath);
            if (!store.TryMarkBeforeMutation(job.Id))
            {
                await Report(client, job.Id, "stopped", [], [], "Job was already processed by this helper.");
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
            return failed.Count == 0 ? 0 : 1;
        }
        catch (OperationCanceledException error)
        {
            await Report(client, job.Id, "stopped", landed, failed, error.Message);
            return 1;
        }
        catch (Exception error)
        {
            await Report(client, job.Id, landed.Count == 0 ? "stopped" : "partial", landed, failed, error.Message);
            return 1;
        }
    }

    private static Task Report(JobClient client, string id, string outcome, List<string> landed, List<string> failed, string reason) =>
        client.CompleteAsync(id, new JobResult { Outcome = outcome, LandedFields = landed, FailedFields = failed, Reason = reason }, CancellationToken.None);

    private static string? ValueAfter(string[] args, string option)
    {
        var index = Array.FindIndex(args, value => string.Equals(value, option, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
