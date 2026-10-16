using System.Security.Cryptography;
using System.Text;

namespace Sverka;

/// <summary>
/// Пишет файловый черновик расширения: Configuration.xml, заимствованные объекты
/// и модули с хуками. Метаданные (реквизиты, формы, новые объекты) не пишет —
/// они только подсвечиваются в реестре.
/// </summary>
public static class ExtensionDraftWriter
{
    private static readonly Dictionary<string, string> FolderByType = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CommonModule"] = "CommonModules",
        ["Catalog"] = "Catalogs",
        ["Document"] = "Documents",
        ["InformationRegister"] = "InformationRegisters",
        ["AccumulationRegister"] = "AccumulationRegisters",
        ["Enum"] = "Enums",
        ["Constant"] = "Constants",
        ["DataProcessor"] = "DataProcessors",
        ["Report"] = "Reports",
    };

    /// <param name="onlyItemKeys">Если задано — пишем только эти пункты (инкремент по «Готово»).</param>
    /// <param name="merge">Не затирать уже существующие методы в .bsl, а дополнять/заменять по имени.</param>
    public static void Write(
        string directory,
        TransferPlan plan,
        IReadOnlySet<string>? onlyItemKeys = null,
        bool merge = true)
    {
        Directory.CreateDirectory(directory);
        WriteConfiguration(directory, plan);

        var autoItems = plan.Items
            .Where(item => item.Decision == TransferDecision.Auto)
            .Where(item => onlyItemKeys == null || onlyItemKeys.Contains(TransferSession.ItemKey(item)))
            .ToList();
        var byModule = autoItems
            .Where(item => !string.IsNullOrEmpty(item.Module))
            .GroupBy(item => (
                Key: item.ObjectUuid ?? item.ObjectName,
                ObjectName: item.ObjectName,
                ObjectType: item.ObjectType,
                Module: item.Module!))
            .OrderBy(group => group.Key.ObjectName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.Key.Module, StringComparer.OrdinalIgnoreCase);

        foreach (var group in byModule)
            WriteBorrowedModule(directory, plan, group.First(), group.ToList(), merge);
    }

    private static void WriteConfiguration(string directory, TransferPlan plan)
    {
        var uuid = DeterministicGuid(plan.ExtensionName, "configuration");
        var content = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <MetaDataObject xmlns="http://v8.1c.ru/8.3/MDClasses" xmlns:app="http://v8.1c.ru/8.2/managed-application/core" xmlns:cfg="http://v8.1c.ru/8.1/data/enterprise/current-config" xmlns:cmi="http://v8.1c.ru/8.2/managed-application/cmi" xmlns:ent="http://v8.1c.ru/8.1/data/enterprise" xmlns:lf="http://v8.1c.ru/8.2/managed-application/logform" xmlns:style="http://v8.1c.ru/8.1/data/ui/style" xmlns:sys="http://v8.1c.ru/8.1/data/ui/fonts/system" xmlns:v8="http://v8.1c.ru/8.1/data/core" xmlns:v8ui="http://v8.1c.ru/8.1/data/ui" xmlns:web="http://v8.1c.ru/8.1/data/ui/colors/web" xmlns:win="http://v8.1c.ru/8.1/data/ui/colors/windows" xmlns:xen="http://v8.1c.ru/8.3/xcf/enums" xmlns:xpr="http://v8.1c.ru/8.3/xcf/predef" xmlns:xr="http://v8.1c.ru/8.3/xcf/readable" xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" version="2.17">
              <Configuration uuid="{uuid}">
                <InternalInfo/>
                <Properties>
                  <ObjectBelonging>Adopted</ObjectBelonging>
                  <Name>{Escape(plan.ExtensionName)}</Name>
                  <NamePrefix>{Escape(plan.Prefix)}</NamePrefix>
                  <ConfigurationExtensionPurpose>Customization</ConfigurationExtensionPurpose>
                  <KeepMappingToExtendedConfigurationObjectsByIDs>true</KeepMappingToExtendedConfigurationObjectsByIDs>
                  <Version>1.0.0.1</Version>
                </Properties>
                <ChildObjects/>
              </Configuration>
            </MetaDataObject>
            """;
        File.WriteAllText(Path.Combine(directory, "Configuration.xml"), content, Utf8NoBom());
    }

    private static void WriteBorrowedModule(
        string directory,
        TransferPlan plan,
        TransferItem sample,
        IReadOnlyList<TransferItem> items,
        bool merge)
    {
        var shortName = ShortName(sample.ObjectName);
        var folder = FolderByType.GetValueOrDefault(sample.ObjectType, sample.ObjectType + "s");
        var objectUuid = DeterministicGuid(plan.ExtensionName, sample.ObjectUuid ?? sample.ObjectName, "object");
        var baseUuid = sample.ObjectUuid ?? "";
        var objectPath = Path.Combine(directory, folder, shortName + ".xml");

        if (!File.Exists(objectPath))
        {
            var objectXml = $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <MetaDataObject>
                  <{sample.ObjectType} uuid="{objectUuid}">
                    <Properties>
                      <Name>{Escape(shortName)}</Name>
                      <ObjectBelonging>Adopted</ObjectBelonging>
                      <ExtendedConfigurationObject>{Escape(baseUuid)}</ExtendedConfigurationObject>
                    </Properties>
                    <ChildObjects/>
                  </{sample.ObjectType}>
                </MetaDataObject>
                """;
            Directory.CreateDirectory(Path.Combine(directory, folder));
            File.WriteAllText(objectPath, objectXml, Utf8NoBom());
        }

        var objectDir = Path.Combine(directory, folder, shortName);
        Directory.CreateDirectory(objectDir);

        var moduleRelative = sample.Module!.Replace('/', Path.DirectorySeparatorChar);
        var modulePath = Path.Combine(objectDir, moduleRelative);
        Directory.CreateDirectory(Path.GetDirectoryName(modulePath)!);

        var incoming = items
            .OrderBy(entry => entry.Method, StringComparer.OrdinalIgnoreCase)
            .Select(item => (Name: MethodNameInDraft(plan.Prefix, item), Text: RenderMethod(plan.Prefix, item)))
            .ToList();

        string content;
        if (merge && File.Exists(modulePath))
        {
            content = MergeModuleFile(File.ReadAllText(modulePath), incoming);
        }
        else
        {
            var builder = new StringBuilder();
            builder.AppendLine("// Черновик расширения, сформированный Сверкой.");
            builder.AppendLine("// Проверьте хуки в Конфигураторе перед загрузкой в рабочую базу.");
            builder.AppendLine();
            foreach (var (_, text) in incoming)
            {
                builder.AppendLine(text);
                builder.AppendLine();
            }

            content = builder.ToString().TrimEnd() + "\n";
        }

        File.WriteAllText(modulePath, content, Utf8NoBom());
    }

    private static string MethodNameInDraft(string prefix, TransferItem item)
    {
        var target = item.Method ?? "Метод";
        if (!string.IsNullOrEmpty(item.AddedMethodSource))
            return prefix + target;
        return prefix + target;
    }

    /// <summary>Заменяет методы с тем же именем, остальные блоки из файла сохраняет.</summary>
    private static string MergeModuleFile(
        string existing,
        IReadOnlyList<(string Name, string Text)> incoming)
    {
        var byName = incoming.ToDictionary(item => item.Name, item => item.Text, StringComparer.OrdinalIgnoreCase);
        var replaced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var blocks = SplitMethodBlocks(existing);
        var output = new List<string>();

        foreach (var block in blocks)
        {
            var name = DetectMethodName(block);
            if (name != null && byName.TryGetValue(name, out var replacement))
            {
                output.Add(replacement.TrimEnd());
                replaced.Add(name);
            }
            else if (!string.IsNullOrWhiteSpace(block))
            {
                output.Add(block.TrimEnd());
            }
        }

        foreach (var (name, text) in incoming)
        {
            if (replaced.Contains(name))
                continue;
            output.Add(text.TrimEnd());
        }

        if (output.Count == 0 || !output[0].Contains("Черновик расширения", StringComparison.Ordinal))
        {
            output.Insert(0, "// Черновик расширения, сформированный Сверкой.\n// Проверьте хуки в Конфигураторе перед загрузкой в рабочую базу.");
        }

        return string.Join("\n\n", output).TrimEnd() + "\n";
    }

    private static List<string> SplitMethodBlocks(string source)
    {
        var lines = BslText.Lines(source);
        var blocks = new List<string>();
        var current = new List<string>();
        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            var startsMethod =
                (trimmed.StartsWith("Процедура ", StringComparison.OrdinalIgnoreCase)
                 || trimmed.StartsWith("Функция ", StringComparison.OrdinalIgnoreCase)
                 || trimmed.StartsWith("Procedure ", StringComparison.OrdinalIgnoreCase)
                 || trimmed.StartsWith("Function ", StringComparison.OrdinalIgnoreCase)
                 || trimmed.StartsWith('&'))
                && current.Count > 0
                && current.Any(entry =>
                {
                    var t = entry.TrimStart();
                    return t.StartsWith("КонецПроцедуры", StringComparison.OrdinalIgnoreCase)
                           || t.StartsWith("КонецФункции", StringComparison.OrdinalIgnoreCase)
                           || t.StartsWith("EndProcedure", StringComparison.OrdinalIgnoreCase)
                           || t.StartsWith("EndFunction", StringComparison.OrdinalIgnoreCase);
                });

            // Новый метод после закрытия предыдущего — или директива перед процедурой.
            if (trimmed.StartsWith('&') || trimmed.StartsWith("Процедура ", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("Функция ", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("Procedure ", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("Function ", StringComparison.OrdinalIgnoreCase))
            {
                var prevClosed = current.Count == 0 || current.Any(entry =>
                {
                    var t = entry.TrimStart();
                    return t.StartsWith("КонецПроцедуры", StringComparison.OrdinalIgnoreCase)
                           || t.StartsWith("КонецФункции", StringComparison.OrdinalIgnoreCase)
                           || t.StartsWith("EndProcedure", StringComparison.OrdinalIgnoreCase)
                           || t.StartsWith("EndFunction", StringComparison.OrdinalIgnoreCase)
                           || t.StartsWith("//", StringComparison.Ordinal);
                });
                // Если текущий блок — только комментарии/пусто, продолжаем копить.
                var onlyPreamble = current.All(entry =>
                    string.IsNullOrWhiteSpace(entry) || entry.TrimStart().StartsWith("//", StringComparison.Ordinal));
                if (!onlyPreamble && current.Count > 0 && prevClosed
                    && current.Any(entry =>
                    {
                        var t = entry.TrimStart();
                        return t.StartsWith("КонецПроцедуры", StringComparison.OrdinalIgnoreCase)
                               || t.StartsWith("КонецФункции", StringComparison.OrdinalIgnoreCase)
                               || t.StartsWith("EndProcedure", StringComparison.OrdinalIgnoreCase)
                               || t.StartsWith("EndFunction", StringComparison.OrdinalIgnoreCase);
                    }))
                {
                    blocks.Add(string.Join("\n", current));
                    current = [];
                }
            }

            current.Add(line);
            _ = startsMethod;
        }

        if (current.Count > 0)
            blocks.Add(string.Join("\n", current));
        return blocks;
    }

    private static string? DetectMethodName(string block)
    {
        foreach (var line in BslText.Lines(block))
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith('&') || trimmed.StartsWith("//", StringComparison.Ordinal))
                continue;
            foreach (var kind in new[] { "Процедура ", "Функция ", "Procedure ", "Function " })
            {
                if (!trimmed.StartsWith(kind, StringComparison.OrdinalIgnoreCase))
                    continue;
                var rest = trimmed[kind.Length..].TrimStart();
                var paren = rest.IndexOf('(');
                if (paren <= 0)
                    return rest.Trim();
                return rest[..paren].Trim();
            }
        }

        return null;
    }

    /// <summary>Текст метода как в черновике расширения — для панели «В расширение».</summary>
    public static string PreviewMethod(string prefix, TransferItem item) => RenderMethod(prefix, item);

    private static string RenderMethod(string prefix, TransferItem item)
    {
        if (!string.IsNullOrEmpty(item.AddedMethodSource))
            return PrefixedOwnMethod(prefix, item);

        var target = item.Method ?? "Метод";
        var hookName = $"{prefix}{target}";
        var header = RewriteHeader(item.MethodHeader ?? $"Процедура {target}()", hookName, item.IsFunction);
        var end = item.IsFunction ? "КонецФункции" : "КонецПроцедуры";
        var body = item.HookBody ?? "";

        return item.ProposedHook switch
        {
            ProposedHook.Before => $"""
                &Перед("{target}")
                {header}
                {Indent(body)}
                {end}
                """,
            ProposedHook.After => $"""
                &После("{target}")
                {header}
                {Indent(body)}
                {end}
                """,
            ProposedHook.Instead => $"""
                &Вместо("{target}")
                {header}
                {Indent(body)}
                	ПродолжитьВызов();
                {end}
                """,
            // HookBody уже содержит полное тело с #Вставка / #КонецВставки.
            ProposedHook.ChangeAndControl => $"""
                &ИзменениеИКонтроль("{target}")
                {header}
                {Indent(body)}
                {end}
                """,
            _ => $"""
                {header}
                {Indent(body)}
                {end}
                """,
        };
    }

    private static string PrefixedOwnMethod(string prefix, TransferItem item)
    {
        var source = item.AddedMethodSource!;
        var lines = BslText.Lines(source).ToList();
        if (lines.Count == 0)
            return source;

        var name = item.Method ?? "Метод";
        lines[0] = RewriteHeader(lines[0], $"{prefix}{name}", item.IsFunction);
        return string.Join("\n", lines);
    }

    private static string RewriteHeader(string header, string newName, bool isFunction)
    {
        var kindRu = isFunction ? "Функция" : "Процедура";
        var kindEn = isFunction ? "Function" : "Procedure";
        var trimmed = header.TrimStart();
        foreach (var kind in new[] { kindRu, kindEn, "Функция", "Процедура", "Function", "Procedure" })
        {
            if (!trimmed.StartsWith(kind, StringComparison.OrdinalIgnoreCase))
                continue;
            var rest = trimmed[kind.Length..].TrimStart();
            var paren = rest.IndexOf('(');
            var args = paren >= 0 ? rest[paren..] : "()";
            var indent = header[..(header.Length - header.TrimStart().Length)];
            return $"{indent}{kind} {newName}{args}";
        }

        return $"{kindRu} {newName}()";
    }

    private static string Indent(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return "\t";
        var lines = BslText.Lines(body);
        return string.Join("\n", lines.Select(line =>
            string.IsNullOrWhiteSpace(line) ? "" :
            line.StartsWith('\t') || line.StartsWith("    ", StringComparison.Ordinal) ? line : "\t" + line));
    }

    private static string ShortName(string objectName) =>
        objectName.Contains('.') ? objectName[(objectName.IndexOf('.') + 1)..] : objectName;

    private static string DeterministicGuid(params string[] parts)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(string.Join("\n", parts)));
        return new Guid(hash).ToString();
    }

    private static string Escape(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);

    private static UTF8Encoding Utf8NoBom() => new(encoderShouldEmitUTF8Identifier: false);
}
