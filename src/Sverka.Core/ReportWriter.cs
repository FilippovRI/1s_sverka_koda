using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sverka;

public static class ReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static void WriteConfig(string path, ConfigurationDump before, ConfigurationDump after, IReadOnlyList<ConfigChange> changes)
    {
        var report = new
        {
            before = Header(before),
            after = Header(after),
            changes = changes.Select(change => new
            {
                @object = change.ObjectName,
                module = change.Module,
                method = change.Method,
                member = change.MemberName == null ? null : $"{change.MemberKind}.{change.MemberName}",
                kind = KindName(change.Kind),
                memberDelta = change.MemberDelta == MemberDelta.None ? null : MemberDeltaName(change.MemberDelta),
                modulePart = change.ModulePart == null ? null : ModulePartName(change.ModulePart.Value),
                summary = change.Summary,
            }),
        };
        WriteJson(path, report);
    }

    public static void WriteImpact(string path, IReadOnlyList<ImpactFinding> findings)
    {
        var report = new
        {
            findings = findings.Select(finding => new
            {
                extension = finding.Extension,
                @object = finding.ObjectName,
                module = finding.Module,
                method = finding.Method,
                intervention = finding.Intervention,
                severity = SeverityName(finding.Severity),
                reason = finding.Reason,
                configChangeKind = finding.ConfigChangeKind == null ? null : KindName(finding.ConfigChangeKind.Value),
            }),
        };
        WriteJson(path, report);
    }

    public static void WriteExtensionSummary(string path, IReadOnlyList<ExtensionSummary> summaries)
    {
        var report = new
        {
            extensions = summaries.Select(summary => new
            {
                extension = summary.Extension,
                critical = summary.Critical,
                adaptation = summary.Adaptation,
                warning = summary.Warning,
                @unchecked = summary.Unchecked,
                total = summary.Total,
            }),
        };
        WriteJson(path, report);
    }

    public static void WriteConfigHtml(string path, IReadOnlyList<ConfigChange> changes)
    {
        var rows = new StringBuilder();
        foreach (var change in changes)
        {
            rows.Append("<tr><td>")
                .Append(Encode(change.ObjectName)).Append("</td><td>")
                .Append(Encode(change.Module)).Append("</td><td>")
                .Append(Encode(change.Method ?? change.MemberName)).Append("</td><td>")
                .Append(Encode(KindName(change.Kind))).Append("</td><td>")
                .Append(Encode(change.Summary)).Append("</td></tr>");
        }

        WriteHtml(path, "Сверка конфигураций", """
            <table>
              <thead><tr><th>Объект</th><th>Модуль</th><th>Метод</th><th>Вид</th><th>Что изменилось</th></tr></thead>
              <tbody>
            """, rows.ToString(), changes.Count);
    }

    public static void WriteImpactHtml(string path, IReadOnlyList<ImpactFinding> findings, IReadOnlyList<ExtensionSummary> summaries)
    {
        var summaryRows = new StringBuilder();
        foreach (var summary in summaries)
        {
            summaryRows.Append("<tr><td>")
                .Append(Encode(summary.Extension)).Append("</td><td>")
                .Append(summary.Critical).Append("</td><td>")
                .Append(summary.Adaptation).Append("</td><td>")
                .Append(summary.Warning).Append("</td><td>")
                .Append(summary.Unchecked).Append("</td><td>")
                .Append(summary.Total).Append("</td></tr>");
        }

        var rows = new StringBuilder();
        foreach (var finding in findings)
        {
            rows.Append("<tr><td>")
                .Append(Encode(finding.Extension)).Append("</td><td>")
                .Append(Encode(finding.ObjectName)).Append("</td><td>")
                .Append(Encode(finding.Method)).Append("</td><td>")
                .Append(Encode(finding.Intervention)).Append("</td><td>")
                .Append(Encode(SeverityName(finding.Severity))).Append("</td><td>")
                .Append(Encode(finding.Reason)).Append("</td></tr>");
        }

        var body = new StringBuilder();
        if (summaries.Count > 0)
        {
            body.Append("<h2>Сводка по расширениям</h2>")
                .Append("<p>Расширений: ").Append(summaries.Count).Append("</p>")
                .Append("<table><thead><tr><th>Расширение</th><th>Критично</th><th>Адаптация</th><th>Предупреждение</th><th>Не проверено</th><th>Всего</th></tr></thead><tbody>")
                .Append(summaryRows)
                .Append("</tbody></table>");
        }

        body.Append("<h2>Находки</h2>")
            .Append("<p>Строк: ").Append(findings.Count).Append("</p>")
            .Append("<table><thead><tr><th>Расширение</th><th>Объект</th><th>Метод</th><th>Вмешательство</th><th>Уровень</th><th>Причина</th></tr></thead><tbody>")
            .Append(rows)
            .Append("</tbody></table>");

        WriteHtmlDocument(path, "Влияние на расширения", body.ToString());
    }

    private static object Header(ConfigurationDump dump) => new
    {
        name = dump.Header.Name,
        version = dump.Header.Version,
        compatibilityMode = dump.Header.CompatibilityMode,
        vendor = dump.Header.Vendor,
    };

    private static void WriteJson(string path, object report)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(report, JsonOptions), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void WriteHtml(string path, string title, string tableStart, string rows, int count)
    {
        var html = $$"""
            <!DOCTYPE html>
            <html lang="ru">
            <head>
              <meta charset="utf-8">
              <title>{{title}}</title>
              <style>
                body { font-family: sans-serif; margin: 24px; }
                table { border-collapse: collapse; width: 100%; }
                th, td { border: 1px solid #ccc; padding: 6px 8px; text-align: left; vertical-align: top; }
                th { background: #f3f3f3; }
              </style>
            </head>
            <body>
              <h1>{{title}}</h1>
              <p>Строк: {{count}}</p>
              {{tableStart}}
              {{rows}}
              </tbody></table>
            </body>
            </html>
            """;
        File.WriteAllText(path, html, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void WriteHtmlDocument(string path, string title, string bodyHtml)
    {
        var html = $$"""
            <!DOCTYPE html>
            <html lang="ru">
            <head>
              <meta charset="utf-8">
              <title>{{title}}</title>
              <style>
                body { font-family: sans-serif; margin: 24px; }
                table { border-collapse: collapse; width: 100%; margin-bottom: 16px; }
                th, td { border: 1px solid #ccc; padding: 6px 8px; text-align: left; vertical-align: top; }
                th { background: #f3f3f3; }
                h2 { margin-top: 24px; }
              </style>
            </head>
            <body>
              <h1>{{title}}</h1>
              {{bodyHtml}}
            </body>
            </html>
            """;
        File.WriteAllText(path, html, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string Encode(string? value) => HtmlEncoder.Default.Encode(value ?? "");

    public static string KindName(ChangeKind kind) => kind switch
    {
        ChangeKind.Added => "Добавлен",
        ChangeKind.Removed => "Удалён",
        ChangeKind.Renamed => "Переименован",
        ChangeKind.Properties => "Свойства",
        ChangeKind.Composition => "Состав",
        ChangeKind.Module => "Модуль",
        _ => kind.ToString(),
    };

    public static string SeverityName(Severity severity) => severity switch
    {
        Severity.Critical => "Критично",
        Severity.Adaptation => "Адаптация",
        Severity.Warning => "Предупреждение",
        Severity.Unchecked => "Не проверено",
        _ => severity.ToString(),
    };

    public static string RunStatusName(RunStatus status) => status switch
    {
        RunStatus.Added => "Новое",
        RunStatus.Removed => "Снятое",
        RunStatus.Kept => "Осталось",
        _ => status.ToString(),
    };

    public static string RunSectionName(RunSection section) => section switch
    {
        RunSection.Config => "Конфигурация",
        RunSection.Impact => "Расширения",
        _ => section.ToString(),
    };

    private static string MemberDeltaName(MemberDelta delta) => delta switch
    {
        MemberDelta.Added => "Добавлен",
        MemberDelta.Removed => "Удалён",
        MemberDelta.Modified => "Изменён",
        _ => delta.ToString(),
    };

    private static string ModulePartName(ModuleChangePart part) => part switch
    {
        ModuleChangePart.Body => "Тело",
        ModuleChangePart.Signature => "Сигнатура",
        ModuleChangePart.Both => "СигнатураИТело",
        _ => part.ToString(),
    };
}
