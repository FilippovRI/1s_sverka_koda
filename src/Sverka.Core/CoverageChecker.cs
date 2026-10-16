using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Sverka;

public enum CoverageStatus
{
    /// <summary>Найдено в черновике расширения.</summary>
    Covered,
    /// <summary>Авто-пункт, в черновике нет следа.</summary>
    Missing,
    /// <summary>Не требует файла в extension/ (подсветка, данные, пропуск) — пока не отмечено в сессии.</summary>
    NotRequired,
    /// <summary>Пользователь отметил «Готово»/«Пропуск» в сессии без требования к файлу.</summary>
    Accepted,
}

public sealed record CoverageItem(
    string Key,
    string ObjectName,
    string? Method,
    TransferDecision Decision,
    CoverageStatus Status,
    string Detail);

public sealed record CoverageReport(
    string ExtensionDirectory,
    IReadOnlyList<CoverageItem> Items)
{
    public int Covered => Items.Count(item => item.Status == CoverageStatus.Covered);
    public int Missing => Items.Count(item => item.Status == CoverageStatus.Missing);
    public int NotRequired => Items.Count(item => item.Status == CoverageStatus.NotRequired);
    public int Accepted => Items.Count(item => item.Status == CoverageStatus.Accepted);
}

/// <summary>
/// Контроль: все ли пункты реестра учтены в черновике extension/ (или приняты в сессии).
/// </summary>
public static class CoverageChecker
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static CoverageReport Analyze(
        TransferPlan plan,
        string extensionDirectory,
        TransferSession? session = null,
        string? formsDirectory = null)
    {
        var draftText = LoadDraftText(extensionDirectory);
        if (!string.IsNullOrEmpty(formsDirectory))
            draftText += "\n" + LoadDraftText(formsDirectory);
        var items = new List<CoverageItem>();
        foreach (var item in plan.Items)
        {
            var key = TransferSession.ItemKey(item);
            var work = session?.GetStatus(key) ?? TransferWorkStatus.Todo;
            items.Add(Classify(item, key, work, plan.Prefix, draftText));
        }

        return new CoverageReport(Path.GetFullPath(extensionDirectory), items);
    }

    public static void WriteReports(string directory, CoverageReport report)
    {
        Directory.CreateDirectory(directory);
        WriteJson(Path.Combine(directory, "coverage.json"), report);
        WriteHtml(Path.Combine(directory, "coverage.html"), report);
    }

    private static CoverageItem Classify(
        TransferItem item,
        string key,
        TransferWorkStatus work,
        string prefix,
        string draftText)
    {
        if (work is TransferWorkStatus.Done or TransferWorkStatus.Skipped)
        {
            return new CoverageItem(
                key,
                item.ObjectName,
                item.Method,
                item.Decision,
                CoverageStatus.Accepted,
                work == TransferWorkStatus.Done
                    ? "Отмечено «Готово» в сессии"
                    : "Отмечено «Пропуск» в сессии");
        }

        if (item.Decision is TransferDecision.Highlight
            or TransferDecision.DataMigration
            or TransferDecision.Skip)
        {
            return new CoverageItem(
                key,
                item.ObjectName,
                item.Method,
                item.Decision,
                CoverageStatus.NotRequired,
                "Не генерируется в extension/ — нужна ручная отметка или отдельный контур");
        }

        if (item.Decision == TransferDecision.Manual)
        {
            return new CoverageItem(
                key,
                item.ObjectName,
                item.Method,
                item.Decision,
                CoverageStatus.Missing,
                "Вручную: нет авто-файла — отметьте Готово после переноса или Пропуск");
        }

        // Auto / FormProgrammatic — ищем след в черновике (extension/ + forms/).
        if (string.IsNullOrWhiteSpace(draftText))
        {
            return new CoverageItem(
                key, item.ObjectName, item.Method, item.Decision,
                CoverageStatus.Missing, "Папка extension/ (и forms/) пуста или не найдена");
        }

        if (item.Decision == TransferDecision.FormProgrammatic)
        {
            var part = FormProgrammaticWriter.PartNameOf(item.Method);
            var shortPart = part.Contains('.') ? part[(part.LastIndexOf('.') + 1)..] : part;
            var prefixed = prefix + shortPart;
            if (draftText.Contains(prefixed, StringComparison.OrdinalIgnoreCase)
                || draftText.Contains(FormProgrammaticWriter.FormNameOf(item.Method), StringComparison.OrdinalIgnoreCase))
            {
                return new CoverageItem(
                    key, item.ObjectName, item.Method, item.Decision,
                    CoverageStatus.Covered, "Найдено в forms/ (программный скелет)");
            }

            return new CoverageItem(
                key, item.ObjectName, item.Method, item.Decision,
                CoverageStatus.Missing, "Скелет формы не найден в forms/");
        }

        if (FoundInDraft(item, prefix, draftText))
        {
            return new CoverageItem(
                key, item.ObjectName, item.Method, item.Decision,
                CoverageStatus.Covered, "Найдено в черновике расширения");
        }

        return new CoverageItem(
            key, item.ObjectName, item.Method, item.Decision,
            CoverageStatus.Missing, "Авто-хук не найден в файлах extension/");
    }

    private static bool FoundInDraft(TransferItem item, string prefix, string draftText)
    {
        var method = item.Method;
        if (string.IsNullOrEmpty(method))
            return draftText.Contains(ShortName(item.ObjectName), StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrEmpty(item.AddedMethodSource))
        {
            var prefixed = prefix + method;
            return ContainsProcedureOrFunction(draftText, prefixed);
        }

        if (ContainsHookDirective(draftText, method))
            return true;

        var hookName = prefix + method;
        return ContainsProcedureOrFunction(draftText, hookName);
    }

    private static bool ContainsHookDirective(string text, string method)
    {
        // &Перед("Имя") / &После / &Вместо / &ИзменениеИКонтроль
        var pattern = $@"&\s*(Перед|После|Вместо|ИзменениеИКонтроль|Before|After|Instead|ChangeAndControl)\s*\(\s*""{Regex.Escape(method)}""\s*\)";
        return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool ContainsProcedureOrFunction(string text, string name)
    {
        var pattern = $@"(?m)^\s*(?:Процедура|Функция|Procedure|Function)\s+{Regex.Escape(name)}\s*\(";
        return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string LoadDraftText(string extensionDirectory)
    {
        if (!Directory.Exists(extensionDirectory))
            return "";

        var builder = new StringBuilder();
        foreach (var path in Directory.EnumerateFiles(extensionDirectory, "*.bsl", SearchOption.AllDirectories))
        {
            try
            {
                builder.AppendLine(File.ReadAllText(path));
            }
            catch (IOException)
            {
                // skip locked
            }
        }

        return builder.ToString();
    }

    private static string ShortName(string fullName)
    {
        var dot = fullName.IndexOf('.');
        return dot > 0 ? fullName[(dot + 1)..] : fullName;
    }

    private static void WriteJson(string path, CoverageReport report)
    {
        var payload = new
        {
            extensionDirectory = report.ExtensionDirectory,
            summary = new
            {
                covered = report.Covered,
                missing = report.Missing,
                notRequired = report.NotRequired,
                accepted = report.Accepted,
                total = report.Items.Count,
            },
            items = report.Items.Select(item => new
            {
                key = item.Key,
                @object = item.ObjectName,
                method = item.Method,
                decision = item.Decision.ToString(),
                status = item.Status.ToString(),
                detail = item.Detail,
            }),
        };
        File.WriteAllText(path, JsonSerializer.Serialize(payload, JsonOptions));
    }

    private static void WriteHtml(string path, CoverageReport report)
    {
        var rows = new StringBuilder();
        foreach (var item in report.Items.OrderBy(entry => entry.Status).ThenBy(entry => entry.ObjectName))
        {
            rows.Append("<tr><td>")
                .Append(Encode(item.ObjectName)).Append("</td><td>")
                .Append(Encode(item.Method)).Append("</td><td>")
                .Append(Encode(item.Decision.ToString())).Append("</td><td>")
                .Append(Encode(StatusName(item.Status))).Append("</td><td>")
                .Append(Encode(item.Detail)).Append("</td></tr>");
        }

        var html = $$"""
            <!DOCTYPE html>
            <html lang="ru"><head><meta charset="utf-8"/><title>Контроль переноса</title>
            <style>
            body{font-family:Segoe UI,sans-serif;margin:24px}
            table{border-collapse:collapse;width:100%}
            th,td{border:1px solid #ccc;padding:6px 8px;text-align:left}
            th{background:#f5f5f5}
            </style></head><body>
            <h1>Контроль переноса в расширение</h1>
            <p>Черновик: {{Encode(report.ExtensionDirectory)}}</p>
            <p>Покрыто: {{report.Covered}}, пропущено/не найдено: {{report.Missing}},
            не требует файла: {{report.NotRequired}}, принято в сессии: {{report.Accepted}}</p>
            <table><thead><tr><th>Объект</th><th>Метод</th><th>Решение</th><th>Статус</th><th>Деталь</th></tr></thead>
            <tbody>{{rows}}</tbody></table>
            </body></html>
            """;
        File.WriteAllText(path, html);
    }

    private static string StatusName(CoverageStatus status) => status switch
    {
        CoverageStatus.Covered => "Покрыто",
        CoverageStatus.Missing => "Не найдено",
        CoverageStatus.NotRequired => "Не требуется",
        CoverageStatus.Accepted => "Принято",
        _ => status.ToString(),
    };

    private static string Encode(string? value) =>
        System.Net.WebUtility.HtmlEncode(value ?? "");
}
