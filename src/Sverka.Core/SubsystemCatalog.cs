using System.Text.Json;
using System.Xml.Linq;

namespace Sverka;

/// <summary>
/// Читает дерево подсистем из файловой выгрузки 1С (Subsystems/*.xml)
/// и строит набор объектов для фильтрации сравнения.
/// </summary>
public static class SubsystemCatalog
{
    public static IReadOnlyList<SubsystemInfo> Read(string dumpRoot)
    {
        var root = Path.Combine(Path.GetFullPath(dumpRoot), "Subsystems");
        if (!Directory.Exists(root))
            return [];

        return ReadFolder(root, parentFullName: null);
    }

    /// <summary>
    /// Объединяет подсистемы типовой и доработанной по полному имени.
    /// Состав Content — объединение обеих сторон.
    /// </summary>
    public static IReadOnlyList<SubsystemInfo> Merge(
        IReadOnlyList<SubsystemInfo> typical,
        IReadOnlyList<SubsystemInfo> customized)
    {
        var byName = new Dictionary<string, SubsystemInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in Flatten(typical).Concat(Flatten(customized)))
        {
            if (byName.TryGetValue(node.FullName, out var existing))
            {
                var content = existing.Content
                    .Concat(node.Content)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                byName[node.FullName] = existing with { Content = content };
            }
            else
            {
                byName[node.FullName] = node with { Children = [] };
            }
        }

        // Восстанавливаем дерево по FullName.
        var all = byName.Values.OrderBy(item => item.FullName, StringComparer.OrdinalIgnoreCase).ToList();
        var roots = new List<SubsystemInfo>();
        var nested = new Dictionary<string, List<SubsystemInfo>>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in all)
        {
            var parent = ParentFullName(node.FullName);
            if (parent == null)
            {
                roots.Add(node);
                continue;
            }

            if (!nested.TryGetValue(parent, out var list))
            {
                list = [];
                nested[parent] = list;
            }

            list.Add(node);
        }

        return roots.Select(root => AttachChildren(root, nested)).ToList();
    }

    public static IEnumerable<SubsystemInfo> Flatten(IEnumerable<SubsystemInfo> roots)
    {
        foreach (var root in roots)
        {
            yield return root;
            foreach (var child in Flatten(root.Children))
                yield return child;
        }
    }

    /// <summary>
    /// Собирает полные имена объектов (Type.Name) из выбранных подсистем.
    /// </summary>
    public static HashSet<string> CollectObjectNames(
        IReadOnlyList<SubsystemInfo> tree,
        IEnumerable<string> selectedFullNames,
        bool includeDescendants)
    {
        var selected = new HashSet<string>(selectedFullNames, StringComparer.OrdinalIgnoreCase);
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in Flatten(tree))
        {
            if (!IsSelected(node.FullName, selected, includeDescendants))
                continue;
            foreach (var item in node.Content)
                result.Add(item);
        }

        return result;
    }

    public static void SaveSelection(string path, TransferScope scope)
    {
        var payload = new
        {
            includeDescendants = scope.IncludeDescendants,
            subsystems = scope.SelectedSubsystemNames,
            extraObjects = scope.ExtraObjectNames.OrderBy(item => item, StringComparer.OrdinalIgnoreCase),
            objects = scope.ObjectNames.OrderBy(item => item, StringComparer.OrdinalIgnoreCase),
        };
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Читает сохранённый выбор. ObjectNames — снимок на момент сохранения;
    /// для актуального состава подсистем лучше пересобрать через Analysis.BuildScope.
    /// </summary>
    public static TransferScope? TryLoadSelection(string path)
    {
        if (!File.Exists(path))
            return null;
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var include = !root.TryGetProperty("includeDescendants", out var includeProp) || includeProp.GetBoolean();
        var subsystems = ReadStringArray(root, "subsystems");
        var extras = ReadStringArray(root, "extraObjects");
        var objects = new HashSet<string>(ReadStringArray(root, "objects"), StringComparer.OrdinalIgnoreCase);
        foreach (var extra in extras)
            objects.Add(extra);
        return new TransferScope(subsystems, objects, include, extras);
    }

    private static List<string> ReadStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.Array)
            return [];
        return prop.EnumerateArray()
            .Select(item => item.GetString() ?? "")
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsSelected(
        string fullName,
        HashSet<string> selected,
        bool includeDescendants)
    {
        if (selected.Contains(fullName))
            return true;
        if (!includeDescendants)
            return false;
        foreach (var item in selected)
        {
            if (fullName.StartsWith(item + ".", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static SubsystemInfo AttachChildren(
        SubsystemInfo node,
        IReadOnlyDictionary<string, List<SubsystemInfo>> nested)
    {
        if (!nested.TryGetValue(node.FullName, out var children))
            return node with { Children = [] };

        var attached = children
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(child => AttachChildren(child, nested))
            .ToList();
        return node with { Children = attached };
    }

    private static string? ParentFullName(string fullName)
    {
        var dot = fullName.LastIndexOf('.');
        return dot > 0 ? fullName[..dot] : null;
    }

    private static List<SubsystemInfo> ReadFolder(string folder, string? parentFullName)
    {
        var result = new List<SubsystemInfo>();
        foreach (var xmlPath in Directory.EnumerateFiles(folder, "*.xml")
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var parsed = TryParse(xmlPath, parentFullName);
            if (parsed == null)
                continue;

            var childFolder = Path.Combine(
                Path.GetDirectoryName(xmlPath)!,
                parsed.Name,
                "Subsystems");
            var children = Directory.Exists(childFolder)
                ? ReadFolder(childFolder, parsed.FullName)
                : [];
            result.Add(parsed with { Children = children });
        }

        return result;
    }

    private static SubsystemInfo? TryParse(string xmlPath, string? parentFullName)
    {
        XDocument document;
        try
        {
            document = XDocument.Load(xmlPath);
        }
        catch (Exception exception) when (exception is System.Xml.XmlException or IOException)
        {
            return null;
        }

        var root = document.Root;
        if (root == null || root.Name.LocalName != "MetaDataObject")
            return null;

        var node = root.Elements().FirstOrDefault(element =>
            element.Name.LocalName.Equals("Subsystem", StringComparison.OrdinalIgnoreCase));
        if (node == null)
            return null;

        var properties = node.Elements().FirstOrDefault(element =>
            element.Name.LocalName.Equals("Properties", StringComparison.OrdinalIgnoreCase));
        var name = Text(properties, "Name");
        if (name.Length == 0)
            name = Path.GetFileNameWithoutExtension(xmlPath);

        var uuid = node.Attributes()
            .FirstOrDefault(attribute => attribute.Name.LocalName.Equals("uuid", StringComparison.OrdinalIgnoreCase))
            ?.Value.Trim() ?? "";

        var fullName = string.IsNullOrEmpty(parentFullName) ? name : parentFullName + "." + name;
        var content = ReadContent(properties);
        return new SubsystemInfo(name, fullName, uuid, content, []);
    }

    private static List<string> ReadContent(XElement? properties)
    {
        var content = properties?.Elements()
            .FirstOrDefault(element => element.Name.LocalName.Equals("Content", StringComparison.OrdinalIgnoreCase));
        if (content == null)
            return [];

        var result = new List<string>();
        foreach (var item in content.Elements())
        {
            var value = item.Value.Trim();
            if (value.Length == 0)
                continue;
            // xr:Item → Catalog.Товары; иногда лежит прямо текстом.
            result.Add(value);
        }

        return result
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string Text(XElement? parent, string localName) =>
        parent?.Elements().FirstOrDefault(element => element.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase))
            ?.Value.Trim() ?? "";
}
