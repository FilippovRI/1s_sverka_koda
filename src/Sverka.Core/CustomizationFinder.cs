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
        string prefix,
        TransferScope? scope = null,
        IReadOnlyList<SubsystemInfo>? subsystems = null,
        bool includeRemovedMetadata = false)
    {
        if (string.IsNullOrWhiteSpace(extensionName))
            throw new ArgumentException("Укажите имя расширения.", nameof(extensionName));
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentException("Укажите префикс расширения.", nameof(prefix));

        var effectiveScope = scope ?? TransferScope.All;
        var tree = subsystems ?? [];
        var allChanges = ConfigComparer.Compare(typical, customized);
        var changes = FilterByScope(allChanges, effectiveScope);
        if (!includeRemovedMetadata)
            changes = ExcludeRemovedMetadata(changes);
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

        return new TransferPlan(
            extensionName.Trim(),
            prefix.Trim(),
            typical,
            customized,
            changes,
            items,
            effectiveScope,
            tree);
    }

    private static IReadOnlyList<ConfigChange> FilterByScope(
        IReadOnlyList<ConfigChange> changes,
        TransferScope scope)
    {
        // Нет фильтра — сравниваем всю конфигурацию.
        if (!scope.HasFilter)
            return changes;

        // Фильтр задан, но итоговый набор объектов пуст.
        if (scope.ObjectNames.Count == 0)
            return [];

        return changes
            .Where(change =>
                !string.Equals(change.ObjectName, "Конфигурация", StringComparison.OrdinalIgnoreCase)
                && scope.ObjectNames.Contains(change.ObjectName))
            .ToList();
    }

    /// <summary>
    /// Удаления типовых объектов/реквизитов скрываем по умолчанию.
    /// Новые объекты (Added без члена) остаются.
    /// </summary>
    private static IReadOnlyList<ConfigChange> ExcludeRemovedMetadata(IReadOnlyList<ConfigChange> changes) =>
        changes
            .Where(change =>
                change.Kind != ChangeKind.Removed
                && change.MemberDelta != MemberDelta.Removed)
            .ToList();

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
            if (IsFormModule(change.Module))
                return null;

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

        // Формы (в т.ч. форма списка) в реестр не кладём — засоряют просмотр.
        if (change.MemberDelta != MemberDelta.None && IsFormRelated(change.MemberKind))
            return null;

        if (change.MemberDelta == MemberDelta.Added)
        {
            if (IsDataMember(change.MemberKind))
            {
                return Item(
                    change,
                    objectType,
                    TransferDecision.DataMigration,
                    ProposedHook.None,
                    "Добавленный реквизит/ТЧ — в черновик миграции данных, XML расширения не пишем",
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
            // Макеты и права в реестре переноса не нужны — только шум.
            if (IsNoisyMember(change.MemberKind))
                return null;

            var beforeMember = FindMember(typicalObject, change.MemberKind, change.MemberName);
            var afterMember = FindMember(customizedObject, change.MemberKind, change.MemberName);
            if (beforeMember != null && afterMember != null
                && string.Equals(beforeMember.EssentialFingerprint, afterMember.EssentialFingerprint, StringComparison.Ordinal))
            {
                // Синоним/подсказка без смены типа — в реестр не выносим.
                return null;
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
            // Модули форм в реестр не кладём — плохо читаются и требуют заимствования формы.
            if (IsFormModule(change.Module))
                return null;

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
            if (proposal.Decision == TransferDecision.Skip)
                return null;

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

        if (change.Kind == ChangeKind.Properties
            && typicalObject != null
            && customizedObject != null
            && string.Equals(
                typicalObject.EssentialPropertiesFingerprint,
                customizedObject.EssentialPropertiesFingerprint,
                StringComparison.Ordinal))
        {
            // Косметика свойств объекта (синоним и т.п.) без отдельных доработок — не показывать.
            return null;
        }

        // Служебные отличия заголовка конфигурации к переносу не относятся.
        if (string.Equals(change.ObjectName, "Конфигурация", StringComparison.OrdinalIgnoreCase))
            return null;

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

    private static bool IsFormModule(string? modulePath) =>
        !string.IsNullOrEmpty(modulePath)
        && (modulePath.Contains("Forms/", StringComparison.OrdinalIgnoreCase)
            || modulePath.Contains(@"Forms\", StringComparison.OrdinalIgnoreCase));

    private static bool IsNoisyMember(string? memberKind) =>
        !string.IsNullOrEmpty(memberKind)
        && (memberKind.Equals("Template", StringComparison.OrdinalIgnoreCase)
            || memberKind.Equals("Rights", StringComparison.OrdinalIgnoreCase)
            || memberKind.Equals("Command", StringComparison.OrdinalIgnoreCase));

    private static bool IsDataMember(string? memberKind) =>
        !string.IsNullOrEmpty(memberKind)
        && (memberKind.Equals("Attribute", StringComparison.OrdinalIgnoreCase)
            || memberKind.Equals("TabularSection", StringComparison.OrdinalIgnoreCase)
            || memberKind.Equals("Dimension", StringComparison.OrdinalIgnoreCase)
            || memberKind.Equals("Resource", StringComparison.OrdinalIgnoreCase)
            || memberKind.Equals("Column", StringComparison.OrdinalIgnoreCase));

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
