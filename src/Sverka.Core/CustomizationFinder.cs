namespace Sverka;

/// <summary>
/// Находит доработки: дифф «типовая → доработанная» и классифицирует каждую
/// для переноса в расширение.
/// </summary>
public static class CustomizationFinder
{
    public static TransferPlan Find(
        ConfigurationDump typical,
        ConfigurationDump customized,
        string extensionName,
        string prefix)
    {
        if (string.IsNullOrWhiteSpace(extensionName))
            throw new ArgumentException("Укажите имя расширения.", nameof(extensionName));
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentException("Укажите префикс расширения.", nameof(prefix));

        var changes = ConfigComparer.Compare(typical, customized);
        var typicalByUuid = IndexByUuid(typical.Objects);
        var customizedByUuid = IndexByUuid(customized.Objects);
        var items = new List<TransferItem>();

        foreach (var change in changes)
        {
            var item = Classify(change, typicalByUuid, customizedByUuid);
            if (item != null)
                items.Add(item);
        }

        items = items
            .OrderBy(item => item.Decision)
            .ThenBy(item => item.ObjectName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Module ?? "", StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Method ?? "", StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Reason, StringComparer.Ordinal)
            .ToList();

        return new TransferPlan(extensionName.Trim(), prefix.Trim(), typical, customized, changes, items);
    }

    private static TransferItem? Classify(
        ConfigChange change,
        IReadOnlyDictionary<string, MetadataObject> typicalByUuid,
        IReadOnlyDictionary<string, MetadataObject> customizedByUuid)
    {
        MetadataObject? typicalObject = null;
        MetadataObject? customizedObject = null;
        if (!string.IsNullOrEmpty(change.ObjectUuid))
        {
            typicalByUuid.TryGetValue(change.ObjectUuid, out typicalObject);
            customizedByUuid.TryGetValue(change.ObjectUuid, out customizedObject);
        }

        var objectType = customizedObject?.Type ?? typicalObject?.Type ?? GuessType(change.ObjectName);

        if (change.Kind == ChangeKind.Added && change.Method != null && change.Module != null)
        {
            var module = FindModule(customizedObject, change.Module);
            var method = module?.Methods.FirstOrDefault(item =>
                item.Name.Equals(change.Method, StringComparison.OrdinalIgnoreCase));
            if (method == null || module == null)
                return Item(change, objectType, TransferDecision.Manual, ProposedHook.None, "Добавленный метод не разобран", null, null, false, null);

            var source = ExtractMethodSource(module.Source, method.Name, method.IsFunction);
            return Item(
                change,
                objectType,
                TransferDecision.Auto,
                ProposedHook.None,
                "Собственный метод — переносится в модуль расширения с префиксом",
                null,
                ExtractHeader(module.Source, method.Name) ?? HeaderOf(method),
                method.IsFunction,
                source);
        }

        if (change.Kind == ChangeKind.Added && change.Method == null && change.MemberName == null)
        {
            return Item(
                change,
                objectType,
                TransferDecision.Highlight,
                ProposedHook.None,
                "Новый объект — только подсветка, в черновик расширения не переносится",
                null, null, false, null);
        }

        if (change.Kind == ChangeKind.Removed)
        {
            return Item(
                change,
                objectType,
                TransferDecision.Manual,
                ProposedHook.None,
                "Удаление типового элемента — автоматически не переносится",
                null, null, false, null);
        }

        if (change.MemberDelta != MemberDelta.None && IsFormRelated(change.MemberKind))
        {
            return Item(
                change,
                objectType,
                TransferDecision.Highlight,
                ProposedHook.None,
                "Форма или элемент формы — только подсветка, не переносится",
                null, null, false, null);
        }

        if (change.MemberDelta == MemberDelta.Added)
        {
            return Item(
                change,
                objectType,
                TransferDecision.Highlight,
                ProposedHook.None,
                "Метаданные (не модуль) — только подсветка, XML не генерируется",
                null, null, false, null);
        }

        if (change.MemberDelta == MemberDelta.Removed)
        {
            return Item(
                change,
                objectType,
                TransferDecision.Highlight,
                ProposedHook.None,
                "Удалённый элемент метаданных — только подсветка",
                null, null, false, null);
        }

        if (change.MemberDelta == MemberDelta.Modified && change.MemberName != null)
        {
            var beforeMember = FindMember(typicalObject, change.MemberKind, change.MemberName);
            var afterMember = FindMember(customizedObject, change.MemberKind, change.MemberName);
            if (beforeMember != null && afterMember != null
                && string.Equals(beforeMember.EssentialFingerprint, afterMember.EssentialFingerprint, StringComparison.Ordinal)
                && !string.Equals(beforeMember.Fingerprint, afterMember.Fingerprint, StringComparison.Ordinal))
            {
                return Item(
                    change,
                    objectType,
                    TransferDecision.Skip,
                    ProposedHook.None,
                    "Косметическое изменение представления",
                    null, null, false, null);
            }

            return Item(
                change,
                objectType,
                TransferDecision.Highlight,
                ProposedHook.None,
                "Метаданные (не модуль) — только подсветка, XML не генерируется",
                null, null, false, null);
        }

        if (change.Kind == ChangeKind.Module && change.Method != null && change.Module != null)
        {
            if (change.ModulePart is ModuleChangePart.Signature or ModuleChangePart.Both)
            {
                return Item(
                    change,
                    objectType,
                    TransferDecision.Manual,
                    ProposedHook.None,
                    "Изменилась сигнатура — хук без ручной проверки небезопасен",
                    null, null, false, null);
            }

            var customModule = FindModule(customizedObject, change.Module);
            var typicalMethod = FindMethod(typicalObject, change.Module, change.Method);
            var customMethod = customModule?.Methods.FirstOrDefault(item =>
                item.Name.Equals(change.Method, StringComparison.OrdinalIgnoreCase));
            if (typicalMethod == null || customMethod == null || customModule == null)
            {
                return Item(
                    change,
                    objectType,
                    TransferDecision.Manual,
                    ProposedHook.None,
                    "Не удалось сопоставить метод в обеих выгрузках",
                    null, null, false, null);
            }

            var proposal = HookSynthesizer.ForBodyChange(typicalMethod, customMethod);
            return Item(
                change,
                objectType,
                proposal.Decision,
                proposal.Hook,
                proposal.Reason,
                proposal.HookBody,
                ExtractHeader(customModule.Source, customMethod.Name) ?? HeaderOf(customMethod),
                customMethod.IsFunction,
                null);
        }

        if (change.Kind is ChangeKind.Renamed or ChangeKind.Properties or ChangeKind.Composition)
        {
            return Item(
                change,
                objectType,
                TransferDecision.Highlight,
                ProposedHook.None,
                "Метаданные (не модуль) — только подсветка: " + change.Summary,
                null, null, false, null);
        }

        return Item(
            change,
            objectType,
            TransferDecision.Highlight,
            ProposedHook.None,
            "Не модуль — только подсветка: " + change.Summary,
            null, null, false, null);
    }

    private static TransferItem Item(
        ConfigChange change,
        string objectType,
        TransferDecision decision,
        ProposedHook hook,
        string reason,
        string? hookBody,
        string? methodHeader,
        bool isFunction,
        string? addedSource) =>
        new(
            change.ObjectName,
            change.ObjectUuid,
            objectType,
            change.Module,
            change.Method ?? change.MemberName,
            change.MemberKind,
            change.Kind,
            decision,
            reason,
            hook,
            hookBody,
            methodHeader,
            isFunction,
            addedSource);

    private static CodeModule? FindModule(MetadataObject? owner, string modulePath) =>
        owner?.Modules.FirstOrDefault(item =>
            item.RelativePath.Equals(modulePath, StringComparison.OrdinalIgnoreCase));

    private static BslMethod? FindMethod(MetadataObject? owner, string modulePath, string methodName) =>
        FindModule(owner, modulePath)?.Methods.FirstOrDefault(item =>
            item.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase));

    private static string? ExtractHeader(string source, string methodName)
    {
        foreach (var line in BslText.Lines(source))
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith('&') || trimmed.StartsWith("//", StringComparison.Ordinal))
                continue;
            if (trimmed.StartsWith("Процедура ", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("Функция ", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("Procedure ", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("Function ", StringComparison.OrdinalIgnoreCase))
            {
                if (trimmed.Contains(methodName, StringComparison.OrdinalIgnoreCase))
                    return line.TrimEnd();
            }
        }

        return null;
    }

    private static string ExtractMethodSource(string source, string methodName, bool isFunction)
    {
        var lines = BslText.Lines(source);
        var endToken = isFunction ? "КонецФункции" : "КонецПроцедуры";
        var endTokenEn = isFunction ? "EndFunction" : "EndProcedure";
        var collecting = false;
        var chunk = new List<string>();
        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            if (!collecting)
            {
                if ((trimmed.StartsWith("Процедура ", StringComparison.OrdinalIgnoreCase)
                     || trimmed.StartsWith("Функция ", StringComparison.OrdinalIgnoreCase)
                     || trimmed.StartsWith("Procedure ", StringComparison.OrdinalIgnoreCase)
                     || trimmed.StartsWith("Function ", StringComparison.OrdinalIgnoreCase))
                    && trimmed.Contains(methodName, StringComparison.OrdinalIgnoreCase))
                {
                    collecting = true;
                    chunk.Add(line);
                }

                continue;
            }

            chunk.Add(line);
            if (trimmed.StartsWith(endToken, StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith(endTokenEn, StringComparison.OrdinalIgnoreCase))
                break;
        }

        return string.Join("\n", chunk);
    }

    private static ObjectMember? FindMember(MetadataObject? owner, string? kind, string name)
    {
        if (owner == null || kind == null)
            return null;
        return owner.Members.FirstOrDefault(item =>
            item.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase)
            && item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private static string HeaderOf(BslMethod method)
    {
        var kind = method.IsFunction ? "Функция" : "Процедура";
        return $"{kind} {method.Name}()";
    }

    private static bool IsFormRelated(string? memberKind) =>
        !string.IsNullOrEmpty(memberKind)
        && (memberKind.Equals("Form", StringComparison.OrdinalIgnoreCase)
            || ConfigComparer.IsFormPart(memberKind));

    private static string GuessType(string fullName)
    {
        var dot = fullName.IndexOf('.');
        return dot > 0 ? fullName[..dot] : "Unknown";
    }

    private static Dictionary<string, MetadataObject> IndexByUuid(IReadOnlyList<MetadataObject> objects)
    {
        var index = new Dictionary<string, MetadataObject>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in objects)
        {
            if (item.Uuid.Length == 0)
                continue;
            index.TryAdd(item.Uuid, item);
        }

        return index;
    }
}
