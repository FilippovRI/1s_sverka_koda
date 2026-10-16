using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sverka;

public enum TransferWorkStatus
{
    Todo,
    Done,
    Skipped,
    Blocked,
}

/// <summary>
/// Прогресс переноса: статусы по строкам реестра, чтобы не начинать с нуля.
/// </summary>
public sealed class TransferSession
{
    public string ExtensionName { get; set; } = "";
    public string Prefix { get; set; } = "";
    public string? TypicalPath { get; set; }
    public string? CustomizedPath { get; set; }
    public string? LastDraftPath { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<TransferSessionEntry> Items { get; set; } = [];

    public TransferWorkStatus GetStatus(string itemKey)
    {
        var entry = Items.FirstOrDefault(item =>
            item.Key.Equals(itemKey, StringComparison.OrdinalIgnoreCase));
        return entry?.Status ?? TransferWorkStatus.Todo;
    }

    public void SetStatus(string itemKey, TransferWorkStatus status, string? note = null)
    {
        var entry = Items.FirstOrDefault(item =>
            item.Key.Equals(itemKey, StringComparison.OrdinalIgnoreCase));
        if (entry == null)
        {
            entry = new TransferSessionEntry { Key = itemKey };
            Items.Add(entry);
        }

        entry.Status = status;
        if (note != null)
            entry.Note = note;
        entry.DoneAt = status is TransferWorkStatus.Done or TransferWorkStatus.Skipped
            ? DateTimeOffset.UtcNow
            : null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public static string ItemKey(TransferItem item) =>
        string.Join("|",
            item.ObjectName,
            item.Module ?? "",
            item.Method ?? "",
            item.MemberKind ?? "",
            item.ChangeKind.ToString(),
            item.Decision.ToString());

    public static string WorkStatusName(TransferWorkStatus status) => status switch
    {
        TransferWorkStatus.Todo => "В работе",
        TransferWorkStatus.Done => "Готово",
        TransferWorkStatus.Skipped => "Пропуск",
        TransferWorkStatus.Blocked => "Блок",
        _ => status.ToString(),
    };
}

public sealed class TransferSessionEntry
{
    public string Key { get; set; } = "";
    public TransferWorkStatus Status { get; set; } = TransferWorkStatus.Todo;
    public string? Note { get; set; }
    public DateTimeOffset? DoneAt { get; set; }
}

public static class TransferSessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static void Save(string path, TransferSession session)
    {
        session.UpdatedAt = DateTimeOffset.UtcNow;
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(path, JsonSerializer.Serialize(session, JsonOptions));
    }

    public static TransferSession? TryLoad(string path)
    {
        if (!File.Exists(path))
            return null;
        return JsonSerializer.Deserialize<TransferSession>(File.ReadAllText(path), JsonOptions);
    }
}
