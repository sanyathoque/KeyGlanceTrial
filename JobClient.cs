using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace KeyGlance.Helper;

public sealed class JobClient(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ImportJob?> ClaimAsync(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync("/claim", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NoContent) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ImportJob>(JsonOptions, cancellationToken);
    }

    public async Task CompleteAsync(string jobId, JobResult result, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync($"/jobs/{Uri.EscapeDataString(jobId)}/result", result, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
