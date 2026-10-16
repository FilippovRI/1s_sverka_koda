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

    public static void Write(string directory, TransferPlan plan)
    {
        Directory.CreateDirectory(directory);
        WriteConfiguration(directory, plan);

        var autoItems = plan.Items.Where(item => item.Decision == TransferDecision.Auto).ToList();
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
            WriteBorrowedModule(directory, plan, group.First(), group.ToList());
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
        IReadOnlyList<TransferItem> items)
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

        var builder = new StringBuilder();
        builder.AppendLine("// Черновик расширения, сформированный Сверкой.");
        builder.AppendLine("// Проверьте хуки в Конфигураторе перед загрузкой в рабочую базу.");
        builder.AppendLine();

        foreach (var item in items.OrderBy(entry => entry.Method, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine(RenderMethod(plan.Prefix, item));
            builder.AppendLine();
        }

        File.WriteAllText(modulePath, builder.ToString().TrimEnd() + "\n", Utf8NoBom());
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
