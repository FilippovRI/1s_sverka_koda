using System.Xml.Linq;

namespace Sverka;

public static class DumpReader
{
    private static readonly HashSet<string> NestedFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Ext",
        "Forms",
        "Templates",
        "Commands",
        "Help",
    };

    /// <summary>
    /// Узлы, которые описывают только представление: их правка не меняет ни тип,
    /// ни состав, ни поведение. Список закрытый, всё остальное считается значимым.
    /// </summary>
    private static readonly HashSet<string> PresentationNodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Synonym",
        "Comment",
        "ToolTip",
        "Explanation",
    };

    public static ConfigurationDump Read(string rootPath)
    {
        var root = Path.GetFullPath(rootPath);
        var configurationPath = Path.Combine(root, "Configuration.xml");
        if (!File.Exists(configurationPath))
            throw new FileNotFoundException("В каталоге нет Configuration.xml.", configurationPath);

        var header = ReadHeader(configurationPath);
        var objects = new List<MetadataObject>();
        foreach (var xmlPath in Directory.EnumerateFiles(root, "*.xml", SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            if (string.Equals(xmlPath, configurationPath, StringComparison.OrdinalIgnoreCase))
                continue;
            if (IsNested(xmlPath))
                continue;

            var metadata = TryReadObject(root, xmlPath);
            if (metadata != null)
                objects.Add(metadata);
        }

        return new ConfigurationDump(root, header, objects);
    }

    public static IReadOnlyList<ConfigurationDump> ReadExtensions(string path)
    {
        var full = Path.GetFullPath(path);
        if (File.Exists(Path.Combine(full, "Configuration.xml")))
            return [Read(full)];

        var dumps = new List<ConfigurationDump>();
        if (Directory.Exists(full))
        {
            foreach (var directory in Directory.EnumerateDirectories(full)
                         .OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
            {
                if (File.Exists(Path.Combine(directory, "Configuration.xml")))
                    dumps.Add(Read(directory));
            }
        }

        if (dumps.Count == 0)
            throw new FileNotFoundException("Не найдены выгрузки расширений с Configuration.xml.", full);

        return dumps;
    }

    private static bool IsNested(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(part => NestedFolders.Contains(part));
    }

    private static ConfigurationHeader ReadHeader(string path)
    {
        var document = Load(path);
        var configuration = document.Root?.Elements().FirstOrDefault(element => element.Name.LocalName == "Configuration")
            ?? throw new InvalidDataException($"В {path} нет элемента Configuration.");
        var properties = Child(configuration, "Properties");
        return new ConfigurationHeader(
            Attribute(configuration, "uuid"),
            Text(properties, "Name"),
            Text(properties, "Version"),
            Text(properties, "CompatibilityMode"),
            Text(properties, "Vendor"));
    }

    private static MetadataObject? TryReadObject(string root, string xmlPath)
    {
        var document = Load(xmlPath);
        var rootElement = document.Root;
        if (rootElement == null || rootElement.Name.LocalName != "MetaDataObject")
            return null;

        var node = rootElement.Elements().FirstOrDefault(element => Attribute(element, "uuid").Length > 0);
        if (node == null || node.Name.LocalName == "Configuration")
            return null;

        var properties = Child(node, "Properties");
        var name = Text(properties, "Name");
        if (name.Length == 0)
            name = Path.GetFileNameWithoutExtension(xmlPath);

        var objectDirectory = Path.Combine(
            Path.GetDirectoryName(xmlPath) ?? root,
            Path.GetFileNameWithoutExtension(xmlPath));
        var members = ReadMembers(node, objectDirectory);
        var extended = Text(properties, "ExtendedConfigurationObject");

        return new MetadataObject(
            Attribute(node, "uuid"),
            node.Name.LocalName,
            name,
            properties == null ? "" : FingerprintProperties(properties),
            properties == null ? "" : FingerprintProperties(properties, skipPresentation: true),
            extended.Length == 0 ? null : extended,
            members,
            ReadModules(objectDirectory));
    }

    private static List<ObjectMember> ReadMembers(XElement node, string objectDirectory)
    {
        var members = new List<ObjectMember>();
        var formParts = new List<ObjectMember>();
        var childObjects = Child(node, "ChildObjects");
        if (childObjects != null)
        {
            foreach (var child in childObjects.Elements())
            {
                var kind = child.Name.LocalName;
                var inline = child.Elements().Any() || Attribute(child, "uuid").Length > 0;
                string memberName;
                string fingerprint;
                string essential;
                if (inline)
                {
                    var properties = Child(child, "Properties");
                    memberName = Text(properties, "Name");
                    if (memberName.Length == 0)
                        memberName = child.Value.Trim();
                    fingerprint = Canonical(child);
                    essential = Canonical(child, skipPresentation: true);
                }
                else
                {
                    memberName = child.Value.Trim();
                    fingerprint = "";
                    essential = "";
                }

                if (memberName.Length == 0)
                    continue;

                if (kind.Equals("Form", StringComparison.OrdinalIgnoreCase))
                {
                    var formPath = Path.Combine(objectDirectory, "Forms", memberName, "Ext", "Form.xml");
                    if (File.Exists(formPath))
                    {
                        var parts = ReadFormParts(formPath, memberName);
                        if (parts == null)
                        {
                            fingerprint = CanonicalFile(formPath);
                            essential = fingerprint;
                        }
                        else
                        {
                            fingerprint = parts.Value.OwnFingerprint;
                            essential = parts.Value.OwnEssential;
                            formParts.AddRange(parts.Value.Members);
                        }
                    }
                }

                members.Add(new ObjectMember(kind, memberName, fingerprint, essential));
            }
        }

        var rightsPath = Path.Combine(objectDirectory, "Ext", "Rights.xml");
        if (File.Exists(rightsPath))
        {
            var rights = CanonicalFile(rightsPath);
            members.Add(new ObjectMember("Rights", "Rights", rights, rights));
        }

        members.AddRange(formParts);
        return members;
    }

    /// <summary>
    /// Разбирает Form.xml на отдельные элементы, команды и реквизиты формы.
    /// Отпечаток самой формы считается без этих веток, поэтому правка одного
    /// поля даёт строку про это поле, а не общее «форма изменилась».
    /// </summary>
    private static (string OwnFingerprint, string OwnEssential, List<ObjectMember> Members)? ReadFormParts(string formPath, string formName)
    {
        XElement? root;
        try
        {
            root = Load(formPath).Root;
        }
        catch (InvalidDataException)
        {
            return null;
        }

        if (root == null)
            return null;

        var members = new List<ObjectMember>();
        CollectFormParts(root, formName, members);
        return (FormCanonical(root), FormCanonical(root, skipPresentation: true), members);
    }

    private static void CollectFormParts(XElement node, string formName, List<ObjectMember> members)
    {
        foreach (var child in node.Elements())
        {
            var kind = FormPartKind(node.Name.LocalName);
            var name = Attribute(child, "name");
            if (kind != null && name.Length > 0)
            {
                members.Add(new ObjectMember(
                    kind,
                    formName + "." + FormPath(child),
                    FormCanonical(child),
                    FormCanonical(child, skipPresentation: true)));
            }

            CollectFormParts(child, formName, members);
        }
    }

    private static string? FormPartKind(string containerName)
    {
        if (containerName.Equals("Items", StringComparison.OrdinalIgnoreCase)
            || containerName.Equals("ChildItems", StringComparison.OrdinalIgnoreCase))
        {
            return "FormItem";
        }

        if (containerName.Equals("Attributes", StringComparison.OrdinalIgnoreCase)
            || containerName.Equals("Columns", StringComparison.OrdinalIgnoreCase))
        {
            return "FormAttribute";
        }

        if (containerName.Equals("Commands", StringComparison.OrdinalIgnoreCase))
            return "FormCommand";

        return null;
    }

    private static bool IsFormContainer(string localName) =>
        localName.Equals("Items", StringComparison.OrdinalIgnoreCase)
        || localName.Equals("ChildItems", StringComparison.OrdinalIgnoreCase)
        || localName.Equals("Attributes", StringComparison.OrdinalIgnoreCase)
        || localName.Equals("Columns", StringComparison.OrdinalIgnoreCase)
        || localName.Equals("Commands", StringComparison.OrdinalIgnoreCase);

    private static string FormPath(XElement element)
    {
        var parts = new List<string>();
        for (var current = element; current != null; current = current.Parent)
        {
            var name = Attribute(current, "name");
            if (name.Length > 0)
                parts.Add(name);
        }

        parts.Reverse();
        return string.Join(".", parts);
    }

    private static string FormCanonical(XElement element, bool skipPresentation = false)
    {
        var builder = new System.Text.StringBuilder();
        Walk(element, builder, skipPresentation);
        return builder.ToString();

        static void Walk(XElement node, System.Text.StringBuilder output, bool skipPresentation)
        {
            output.Append(node.Name.LocalName);
            output.Append('{');
            foreach (var attribute in node.Attributes()
                         .Where(item => !item.Name.LocalName.Equals("uuid", StringComparison.OrdinalIgnoreCase)
                             && !item.Name.LocalName.Equals("id", StringComparison.OrdinalIgnoreCase))
                         .OrderBy(item => item.Name.LocalName, StringComparer.Ordinal))
            {
                output.Append(attribute.Name.LocalName);
                output.Append('=');
                output.Append(NormalizeSpace(attribute.Value));
                output.Append(';');
            }

            if (!node.HasElements)
            {
                output.Append(NormalizeSpace(node.Value));
            }
            else
            {
                foreach (var child in node.Elements().Where(item => !IsFormContainer(item.Name.LocalName)))
                {
                    if (skipPresentation && PresentationNodes.Contains(child.Name.LocalName))
                        continue;
                    Walk(child, output, skipPresentation);
                }
            }

            output.Append('}');
        }
    }

    private static List<CodeModule> ReadModules(string objectDirectory)
    {
        if (!Directory.Exists(objectDirectory))
            return [];

        var modules = new List<CodeModule>();
        foreach (var file in Directory.EnumerateFiles(objectDirectory, "*.bsl", SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var relative = Path.GetRelativePath(objectDirectory, file);
            modules.Add(BslParser.Parse(relative, File.ReadAllText(file)));
        }

        return modules;
    }

    private static string FingerprintProperties(XElement properties, bool skipPresentation = false)
    {
        var copy = new XElement(properties);
        foreach (var name in copy.Elements().Where(element => element.Name.LocalName == "Name").ToList())
            name.Remove();
        return Canonical(copy, skipPresentation);
    }

    private static string CanonicalFile(string path)
    {
        try
        {
            var document = Load(path);
            return document.Root == null ? "" : Canonical(document.Root);
        }
        catch (InvalidDataException)
        {
            return File.ReadAllText(path);
        }
    }

    private static string Canonical(XElement element, bool skipPresentation = false)
    {
        var builder = new System.Text.StringBuilder();
        Walk(element, builder, skipPresentation);
        return builder.ToString();

        static void Walk(XElement node, System.Text.StringBuilder output, bool skipPresentation)
        {
            output.Append(node.Name.LocalName);
            output.Append('{');
            foreach (var attribute in node.Attributes()
                         .Where(item => !item.Name.LocalName.Equals("uuid", StringComparison.OrdinalIgnoreCase))
                         .OrderBy(item => item.Name.LocalName, StringComparer.Ordinal))
            {
                output.Append(attribute.Name.LocalName);
                output.Append('=');
                output.Append(NormalizeSpace(attribute.Value));
                output.Append(';');
            }

            if (!node.HasElements)
            {
                output.Append(NormalizeSpace(node.Value));
            }
            else
            {
                foreach (var child in node.Elements())
                {
                    if (skipPresentation && PresentationNodes.Contains(child.Name.LocalName))
                        continue;
                    Walk(child, output, skipPresentation);
                }
            }

            output.Append('}');
        }
    }

    private static string NormalizeSpace(string value)
    {
        var parts = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", parts);
    }

    private static XDocument Load(string path)
    {
        try
        {
            return XDocument.Load(path);
        }
        catch (Exception exception) when (exception is System.Xml.XmlException or IOException)
        {
            throw new InvalidDataException($"Не разобран XML: {path}", exception);
        }
    }

    private static XElement? Child(XElement? parent, string localName) =>
        parent?.Elements().FirstOrDefault(element => element.Name.LocalName == localName);

    private static string Text(XElement? parent, string localName) =>
        Child(parent, localName)?.Value.Trim() ?? "";

    private static string Attribute(XElement element, string localName) =>
        element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase))?.Value.Trim()
        ?? "";
}
