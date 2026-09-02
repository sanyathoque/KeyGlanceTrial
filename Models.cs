using System.Text.Json.Serialization;

namespace KeyGlance.Helper;

public sealed class ImportJob
{
    public required string Id { get; init; }
    public required string Client { get; init; }
    public int Year { get; init; }
    public required Dictionary<string, string> Fields { get; init; }
}

public sealed class JobResult
{
    [JsonPropertyName("outcome")] public required string Outcome { get; init; }
    [JsonPropertyName("landedFields")] public required List<string> LandedFields { get; init; }
    [JsonPropertyName("failedFields")] public required List<string> FailedFields { get; init; }
    [JsonPropertyName("reason")] public required string Reason { get; init; }
}
