namespace Sverka;

public static class ConfigComparer
{
    public static IReadOnlyList<ConfigChange> Compare(ConfigurationDump before, ConfigurationDump after)
    {
        var changes = new List<ConfigChange>();
        CompareHeader(before.Header, after.Header, changes);

        var beforeByUuid = IndexByUuid(before.Objects);
        var usedBefore = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedAfter = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pairs = new List<(MetadataObject Before, MetadataObject After)>();

        foreach (var candidate in after.Objects.OrderBy(item => item.FullName, StringComparer.OrdinalIgnoreCase))
        {
            if (candidate.Uuid.Length == 0 || !beforeByUuid.TryGetValue(candidate.Uuid, out var match))
                continue;
            pairs.Add((match, candidate));
            usedBefore.Add(Key(match));
            usedAfter.Add(Key(candidate));
        }

        var beforeByName = before.Objects
            .Where(item => !usedBefore.Contains(Key(item)))
            .GroupBy(item => item.FullName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in after.Objects.OrderBy(item => item.FullName, StringComparer.OrdinalIgnoreCase))
        {
            if (usedAfter.Contains(Key(candidate)))
                continue;
            if (!beforeByName.TryGetValue(candidate.FullName, out var match))
                continue;
            if (match.Uuid.Length > 0 && candidate.Uuid.Length > 0)
                continue;
            pairs.Add((match, candidate));
            usedBefore.Add(Key(match));
            usedAfter.Add(Key(candidate));
            beforeByName.Remove(candidate.FullName);
        }

        foreach (var item in before.Objects.Where(item => !usedBefore.Contains(Key(item))))
        {
            changes.Add(Change(item.FullName, item.Uuid, null, null, null, null, ChangeKind.Removed, MemberDelta.None, null, "Объект удалён"));
        }

        foreach (var item in after.Objects.Where(item => !usedAfter.Contains(Key(item))))
        {
            changes.Add(Change(item.FullName, item.Uuid, null, null, null, null, ChangeKind.Added, MemberDelta.None, null, "Объект добавлен"));
        }

        foreach (var (left, right) in pairs)
            CompareObject(left, right, changes);

        return changes
            .OrderBy(item => item.ObjectName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Module ?? "", StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Method ?? "", StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.MemberKind ?? "", StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.MemberName ?? "", StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Kind)
            .ThenBy(item => item.Summary, StringComparer.Ordinal)
            .ToList();
    }

    private static void CompareHeader(ConfigurationHeader before, ConfigurationHeader after, List<ConfigChange> changes)
    {
        var uuid = after.Uuid.Length > 0 ? after.Uuid : before.Uuid;
        if (!string.Equals(before.Name, after.Name, StringComparison.Ordinal))
            changes.Add(Change("Конфигурация", uuid, null, null, null, null, ChangeKind.Renamed, MemberDelta.None, null, $"Имя: {before.Name} → {after.Name}"));
        if (!string.Equals(before.Version, after.Version, StringComparison.Ordinal))
            changes.Add(Change("Конфигурация", uuid, null, null, null, null, ChangeKind.Properties, MemberDelta.None, null, $"Версия: {before.Version} → {after.Version}"));
        if (!string.Equals(before.CompatibilityMode, after.CompatibilityMode, StringComparison.Ordinal))
            changes.Add(Change("Конфигурация", uuid, null, null, null, null, ChangeKind.Properties, MemberDelta.None, null, $"Режим совместимости: {before.CompatibilityMode} → {after.CompatibilityMode}"));
        if (!string.Equals(before.Vendor, after.Vendor, StringComparison.Ordinal))
            changes.Add(Change("Конфигурация", uuid, null, null, null, null, ChangeKind.Properties, MemberDelta.None, null, $"Поставщик: {before.Vendor} → {after.Vendor}"));
    }

    private static void CompareObject(MetadataObject before, MetadataObject after, List<ConfigChange> changes)
    {
        if (!string.Equals(before.Name, after.Name, StringComparison.Ordinal))
        {
            changes.Add(Change(after.FullName, after.Uuid, null, null, null, null, ChangeKind.Renamed, MemberDelta.None, null, $"Переименован из {before.FullName}"));
        }

        if (!string.Equals(before.PropertiesFingerprint, after.PropertiesFingerprint, StringComparison.Ordinal))
        {
            changes.Add(Change(after.FullName, after.Uuid, null, null, null, null, ChangeKind.Properties, MemberDelta.Modified, null, "Изменились свойства объекта"));
        }

        CompareMembers(before, after, changes);
        CompareModules(before, after, changes);
    }

    private static void CompareMembers(MetadataObject before, MetadataObject after, List<ConfigChange> changes)
    {
        var beforeMembers = IndexMembers(before.Members);
        var afterMembers = IndexMembers(after.Members);
        foreach (var pair in beforeMembers)
        {
            if (afterMembers.ContainsKey(pair.Key))
                continue;
            var member = pair.Value;
            changes.Add(Change(
                after.FullName,
                after.Uuid,
                null,
                null,
                member.Kind,
                member.Name,
                ChangeKind.Composition,
                MemberDelta.Removed,
                null,
                $"{Verb(member.Kind, MemberDelta.Removed)} {MemberLabel(member.Kind)} {member.Name}"));
        }

        foreach (var pair in afterMembers)
        {
            if (!beforeMembers.TryGetValue(pair.Key, out var previous))
            {
                var member = pair.Value;
                changes.Add(Change(
                    after.FullName,
                    after.Uuid,
                    null,
                    null,
                    member.Kind,
                    member.Name,
                    ChangeKind.Composition,
                    MemberDelta.Added,
                    null,
                    $"{Verb(member.Kind, MemberDelta.Added)} {MemberLabel(member.Kind)} {member.Name}"));
                continue;
            }

            var current = pair.Value;
            if (string.Equals(previous.Fingerprint, current.Fingerprint, StringComparison.Ordinal))
                continue;

            var formLike = current.Kind.Equals("Form", StringComparison.OrdinalIgnoreCase)
                || current.Kind.Equals("Rights", StringComparison.OrdinalIgnoreCase)
                || current.Kind.Equals("Template", StringComparison.OrdinalIgnoreCase)
                || IsFormPart(current.Kind);
            changes.Add(Change(
                after.FullName,
                after.Uuid,
                null,
                null,
                current.Kind,
                current.Name,
                formLike ? ChangeKind.Composition : ChangeKind.Properties,
                MemberDelta.Modified,
                null,
                formLike
                    ? $"{Verb(current.Kind, MemberDelta.Modified)} {MemberLabel(current.Kind)} {current.Name}"
                    : $"Изменились свойства: {MemberLabel(current.Kind)} {current.Name}"));
        }
    }

    private static void CompareModules(MetadataObject before, MetadataObject after, List<ConfigChange> changes)
    {
        var beforeModules = before.Modules.ToDictionary(module => module.RelativePath, StringComparer.OrdinalIgnoreCase);
        var afterModules = after.Modules.ToDictionary(module => module.RelativePath, StringComparer.OrdinalIgnoreCase);

        foreach (var pair in beforeModules)
        {
            if (afterModules.ContainsKey(pair.Key))
                continue;
            foreach (var method in pair.Value.Methods)
            {
                changes.Add(Change(after.FullName, after.Uuid, pair.Key, method.Name, null, null, ChangeKind.Removed, MemberDelta.None, null, "Удалён метод вместе с модулем"));
            }

            if (pair.Value.Methods.Count == 0)
                changes.Add(Change(after.FullName, after.Uuid, pair.Key, null, null, null, ChangeKind.Removed, MemberDelta.None, null, "Удалён модуль"));
        }

        foreach (var pair in afterModules)
        {
            if (!beforeModules.TryGetValue(pair.Key, out var previous))
            {
                foreach (var method in pair.Value.Methods)
                {
                    changes.Add(Change(after.FullName, after.Uuid, pair.Key, method.Name, null, null, ChangeKind.Added, MemberDelta.None, null, "Добавлен метод"));
                }

                if (pair.Value.Methods.Count == 0)
                    changes.Add(Change(after.FullName, after.Uuid, pair.Key, null, null, null, ChangeKind.Added, MemberDelta.None, null, "Добавлен модуль"));
                continue;
            }

            CompareMethods(after, pair.Key, previous, pair.Value, changes);
        }
    }

    private static void CompareMethods(
        MetadataObject after,
        string relativePath,
        CodeModule before,
        CodeModule current,
        List<ConfigChange> changes)
    {
        var beforeMethods = before.Methods
            .GroupBy(method => method.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var afterMethods = current.Methods
            .GroupBy(method => method.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var pair in beforeMethods)
        {
            if (afterMethods.ContainsKey(pair.Key))
                continue;
            changes.Add(Change(after.FullName, after.Uuid, relativePath, pair.Value.Name, null, null, ChangeKind.Removed, MemberDelta.None, null, "Удалён метод"));
        }

        foreach (var pair in afterMethods)
        {
            if (!beforeMethods.TryGetValue(pair.Key, out var previous))
            {
                changes.Add(Change(after.FullName, after.Uuid, relativePath, pair.Value.Name, null, null, ChangeKind.Added, MemberDelta.None, null, "Добавлен метод"));
                continue;
            }

            var signatureChanged = !string.Equals(previous.SignatureNormalized, pair.Value.SignatureNormalized, StringComparison.Ordinal);
            var bodyChanged = !string.Equals(previous.BodyNormalized, pair.Value.BodyNormalized, StringComparison.Ordinal);
            if (!signatureChanged && !bodyChanged)
                continue;

            var part = signatureChanged && bodyChanged
                ? ModuleChangePart.Both
                : signatureChanged ? ModuleChangePart.Signature : ModuleChangePart.Body;
            var summary = part switch
            {
                ModuleChangePart.Both => "Изменились сигнатура и тело",
                ModuleChangePart.Signature => "Изменилась сигнатура",
                _ => "Изменилось тело",
            };
            changes.Add(Change(after.FullName, after.Uuid, relativePath, pair.Value.Name, null, null, ChangeKind.Module, MemberDelta.None, part, summary));
        }
    }

    private static Dictionary<string, ObjectMember> IndexMembers(IReadOnlyList<ObjectMember> members)
    {
        return members
            .GroupBy(member => member.Kind + "\n" + member.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, MetadataObject> IndexByUuid(IReadOnlyList<MetadataObject> objects)
    {
        var index = new Dictionary<string, MetadataObject>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in objects.OrderBy(item => item.FullName, StringComparer.OrdinalIgnoreCase))
        {
            if (item.Uuid.Length == 0)
                continue;
            index.TryAdd(item.Uuid, item);
        }

        return index;
    }

    private static string Key(MetadataObject item) => item.Uuid + "\n" + item.FullName;

    public static bool IsFormPart(string kind) =>
        kind.Equals("FormItem", StringComparison.OrdinalIgnoreCase)
        || kind.Equals("FormCommand", StringComparison.OrdinalIgnoreCase)
        || kind.Equals("FormAttribute", StringComparison.OrdinalIgnoreCase);

    private static string MemberLabel(string kind) => kind.ToLowerInvariant() switch
    {
        "attribute" => "реквизит",
        "tabularsection" => "табличная часть",
        "form" => "форма",
        "formitem" => "элемент формы",
        "formcommand" => "команда формы",
        "formattribute" => "реквизит формы",
        "dimension" => "измерение",
        "resource" => "ресурс",
        "enumvalue" => "значение",
        "rights" => "права",
        "template" => "макет",
        _ => kind,
    };

    private static string Verb(string kind, MemberDelta delta)
    {
        var gender = kind.ToLowerInvariant() switch
        {
            "tabularsection" or "form" or "formcommand" => Gender.Feminine,
            "dimension" or "enumvalue" => Gender.Neuter,
            "rights" => Gender.Plural,
            _ => Gender.Masculine,
        };

        return delta switch
        {
            MemberDelta.Added => gender switch
            {
                Gender.Feminine => "Добавлена",
                Gender.Neuter => "Добавлено",
                Gender.Plural => "Добавлены",
                _ => "Добавлен",
            },
            MemberDelta.Removed => gender switch
            {
                Gender.Feminine => "Удалена",
                Gender.Neuter => "Удалено",
                Gender.Plural => "Удалены",
                _ => "Удалён",
            },
            _ => gender switch
            {
                Gender.Feminine => "Изменилась",
                Gender.Neuter => "Изменилось",
                Gender.Plural => "Изменились",
                _ => "Изменился",
            },
        };
    }

    private enum Gender
    {
        Masculine,
        Feminine,
        Neuter,
        Plural,
    }

    private static ConfigChange Change(
        string objectName,
        string? objectUuid,
        string? module,
        string? method,
        string? memberKind,
        string? memberName,
        ChangeKind kind,
        MemberDelta memberDelta,
        ModuleChangePart? modulePart,
        string summary) =>
        new(objectName, NullIfEmpty(objectUuid), module, method, memberKind, memberName, kind, memberDelta, modulePart, summary);

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
