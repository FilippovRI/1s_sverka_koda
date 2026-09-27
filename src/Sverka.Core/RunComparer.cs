using System.Text.Json;

namespace Sverka;

public enum RunSection
{
    Config,
    Impact,
}

public enum RunStatus
{
    Added,
    Removed,
    Kept,
}

public sealed record RunEntry(
    RunSection Section,
    string? Extension,
    string ObjectName,
    string? Module,
    string? Method,
    string? Member,
    string Kind,
    string Summary)
{
    public string Target => Method ?? Member ?? "";

    public string Key => string.Join(
        "\u0001",
        Section.ToString(),
        Lower(Extension),
        Lower(ObjectName),
        Lower(Module),
        Lower(Method),
        Lower(Member),
        Lower(Kind),
        Summary.Trim());

    private static string Lower(string? value) =>
        (value ?? "").Trim().ToLowerInvariant();
}

public sealed record SavedRun(
    string Path,
    string? ConfigFile,
    string? ImpactFile,
    string? BeforeName,
    string? BeforeVersion,
    string? AfterName,
    string? AfterVersion,
    IReadOnlyList<RunEntry> Entries)
{
    public bool HasImpact => ImpactFile != null;

    public string Title
    {
        get
        {
            if (AfterName == null && AfterVersion == null)
                return Path;
            var before = string.IsNullOrWhiteSpace(BeforeVersion) ? "?" : BeforeVersion;
            var after = string.IsNullOrWhiteSpace(AfterVersion) ? "?" : AfterVersion;
            return $"{AfterName ?? BeforeName ?? "?"} {before} → {after}";
        }
    }
}

public sealed record RunDelta(RunStatus Status, RunEntry Entry);

/// <summary>
/// Сравнивает два ранее сохранённых прогона: читает config.json и impact.json,
/// которые пишет <see cref="ReportWriter"/>, и показывает, что появилось,
/// что снято и что осталось между обновлениями.
/// </summary>
public static class RunComparer
{
    public static SavedRun Load(string path)
    {
        var full = System.IO.Path.GetFullPath(path);
        string? configFile = null;
        string? impactFile = null;

        if (Directory.Exists(full))
        {
            configFile = Existing(System.IO.Path.Combine(full, "config.json"));
            impactFile = Existing(System.IO.Path.Combine(full, "impact.json"));
        }
        else if (File.Exists(full))
        {
            var directory = System.IO.Path.GetDirectoryName(full) ?? full;
            if (IsImpactReport(full))
            {
                impactFile = full;
                configFile = Existing(System.IO.Path.Combine(directory, "config.json"));
            }
            else
            {
                configFile = full;
                impactFile = Existing(System.IO.Path.Combine(directory, "impact.json"));
            }
        }
        else
        {
            throw new FileNotFoundException(
                "Не найден сохранённый прогон: нужен каталог с config.json или сам файл config.json.",
                full);
        }

        if (configFile == null && impactFile == null)
        {
            throw new FileNotFoundException(
                "В указанном месте нет ни config.json, ни impact.json.",
                full);
        }

        var entries = new List<RunEntry>();
        string? beforeName = null;
        string? beforeVersion = null;
        string? afterName = null;
        string? afterVersion = null;

        if (configFile != null)
        {
            using var document = Parse(configFile);
            var root = document.RootElement;
            ReadHeader(root, "before", ref beforeName, ref beforeVersion);
            ReadHeader(root, "after", ref afterName, ref afterVersion);
            if (root.TryGetProperty("changes", out var changes) && changes.ValueKind == JsonValueKind.Array)
            {
                foreach (var change in changes.EnumerateArray())
                {
                    entries.Add(new RunEntry(
                        RunSection.Config,
                        null,
                        Text(change, "object") ?? "",
                        Text(change, "module"),
                        Text(change, "method"),
                        Text(change, "member"),
                        Kind(change),
                        Text(change, "summary") ?? ""));
                }
            }
        }

        if (impactFile != null)
        {
            using var document = Parse(impactFile);
            var root = document.RootElement;
            if (root.TryGetProperty("findings", out var findings) && findings.ValueKind == JsonValueKind.Array)
            {
                foreach (var finding in findings.EnumerateArray())
                {
                    entries.Add(new RunEntry(
                        RunSection.Impact,
                        Text(finding, "extension"),
                        Text(finding, "object") ?? "",
                        Text(finding, "module"),
                        Text(finding, "method"),
                        Text(finding, "intervention"),
                        Text(finding, "severity") ?? "",
                        Text(finding, "reason") ?? ""));
                }
            }
        }

        return new SavedRun(
            full,
            configFile,
            impactFile,
            beforeName,
            beforeVersion,
            afterName,
            afterVersion,
            Order(entries));
    }

    public static IReadOnlyList<RunDelta> Compare(SavedRun previous, SavedRun current)
    {
        var previousCounts = Count(previous.Entries);
        var currentCounts = Count(current.Entries);
        var deltas = new List<RunDelta>();

        foreach (var pair in currentCounts)
        {
            var was = previousCounts.TryGetValue(pair.Key, out var old) ? old.Count : 0;
            var kept = Math.Min(was, pair.Value.Count);
            for (var index = 0; index < kept; index++)
                deltas.Add(new RunDelta(RunStatus.Kept, pair.Value.Entry));
            for (var index = kept; index < pair.Value.Count; index++)
                deltas.Add(new RunDelta(RunStatus.Added, pair.Value.Entry));
        }

        foreach (var pair in previousCounts)
        {
            var now = currentCounts.TryGetValue(pair.Key, out var fresh) ? fresh.Count : 0;
            for (var index = now; index < pair.Value.Count; index++)
                deltas.Add(new RunDelta(RunStatus.Removed, pair.Value.Entry));
        }

        return deltas
            .OrderBy(item => item.Status)
            .ThenBy(item => item.Entry.Section)
            .ThenBy(item => item.Entry.Extension ?? "", StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Entry.ObjectName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Entry.Module ?? "", StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Entry.Method ?? "", StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Entry.Member ?? "", StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Entry.Kind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Entry.Summary, StringComparer.Ordinal)
            .ToList();
    }

    public static IReadOnlyList<RunDelta> Compare(string previousPath, string currentPath) =>
        Compare(Load(previousPath), Load(currentPath));

    private static IReadOnlyList<RunEntry> Order(List<RunEntry> entries) =>
        entries
            .OrderBy(item => item.Section)
            .ThenBy(item => item.Extension ?? "", StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ObjectName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Module ?? "", StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Method ?? "", StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Member ?? "", StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Kind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Summary, StringComparer.Ordinal)
            .ToList();

    private static Dictionary<string, (RunEntry Entry, int Count)> Count(IReadOnlyList<RunEntry> entries)
    {
        var counts = new Dictionary<string, (RunEntry Entry, int Count)>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (counts.TryGetValue(entry.Key, out var existing))
                counts[entry.Key] = (existing.Entry, existing.Count + 1);
            else
                counts[entry.Key] = (entry, 1);
        }

        return counts;
    }

    private static string Kind(JsonElement change)
    {
        var kind = Text(change, "kind") ?? "";
        var part = Text(change, "modulePart");
        var delta = Text(change, "memberDelta");
        if (part != null)
            return $"{kind} ({part})";
        if (delta != null)
            return $"{kind} ({delta})";
        return kind;
    }

    private static void ReadHeader(JsonElement root, string name, ref string? title, ref string? version)
    {
        if (!root.TryGetProperty(name, out var header) || header.ValueKind != JsonValueKind.Object)
            return;
        title = Text(header, "name");
        version = Text(header, "version");
    }

    private static bool IsImpactReport(string file)
    {
        using var document = Parse(file);
        return document.RootElement.ValueKind == JsonValueKind.Object
            && !document.RootElement.TryGetProperty("changes", out _)
            && document.RootElement.TryGetProperty("findings", out _);
    }

    private static JsonDocument Parse(string file)
    {
        try
        {
            return JsonDocument.Parse(File.ReadAllText(file));
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Не разобран отчёт: {file}", exception);
        }
    }

    private static string? Existing(string path) => File.Exists(path) ? path : null;

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
