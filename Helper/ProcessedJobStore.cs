using System.IO;
using System.Text.Json;

namespace KeyGlance.Helper;

public sealed class ProcessedJobStore
{
    private readonly string path;
    private readonly object sync = new();
    private HashSet<string>? processed;

    public ProcessedJobStore(string path) => this.path = path;

    public bool TryMarkBeforeMutation(string jobId)
    {
        lock (sync)
        {
            processed ??= Load();
            if (!processed.Add(jobId)) return false;

            var temporary = path + ".tmp";
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(temporary, JsonSerializer.Serialize(processed.OrderBy(value => value, StringComparer.Ordinal)));
            File.Move(temporary, path, overwrite: true);
            return true;
        }
    }

    private HashSet<string> Load()
    {
        if (!File.Exists(path)) return new(StringComparer.Ordinal);
        var values = JsonSerializer.Deserialize<string[]>(File.ReadAllText(path)) ?? [];
        return new(values, StringComparer.Ordinal);
    }
}
