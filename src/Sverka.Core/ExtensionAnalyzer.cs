namespace Sverka;

public static class ExtensionAnalyzer
{
    public static IReadOnlyList<ImpactFinding> Analyze(
        ConfigurationDump before,
        ConfigurationDump after,
        IReadOnlyList<ConfigChange> changes,
        IReadOnlyList<ConfigurationDump> extensions)
    {
        var findings = new List<ImpactFinding>();
        var commonModules = CommonModuleNames(before, after);
        foreach (var extension in extensions)
        {
            foreach (var extensionObject in extension.Objects)
            {
                var baseObject = ResolveBase(extensionObject, before, after);
                if (baseObject == null)
                    continue;

                var objectChanges = changes.Where(change => SameObject(change, baseObject)).ToList();
                AnalyzeHooks(extension.Header.Name, extensionObject, baseObject, objectChanges, before, after, findings);
                AnalyzeMembers(extension.Header.Name, extensionObject, baseObject, objectChanges, before, after, findings);
                AnalyzeDynamic(extension.Header.Name, extensionObject, baseObject, findings);
            }

            AnalyzeCalls(extension, before, after, commonModules, changes, findings);
        }

        return findings
            .OrderBy(item => item.Extension, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ObjectName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Module ?? "", StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Method ?? "", StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Severity)
            .ThenBy(item => item.Intervention, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Сводка по расширениям для пакетного прогона. В список входят все переданные
    /// расширения, даже без находок. Порядок детерминирован: сначала расширения с
    /// критичными, затем адаптациями, предупреждениями, непроверенными; при равенстве
    /// — по имени. «Чистые» расширения оказываются в конце.
    /// </summary>
    public static IReadOnlyList<ExtensionSummary> Summarize(
        IEnumerable<string> extensionNames,
        IReadOnlyList<ImpactFinding> findings)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in extensionNames)
        {
            if (!string.IsNullOrWhiteSpace(name))
                names.Add(name);
        }

        foreach (var finding in findings)
            names.Add(finding.Extension);

        var summaries = new List<ExtensionSummary>();
        foreach (var name in names)
        {
            var own = findings.Where(item => item.Extension.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
            summaries.Add(new ExtensionSummary(
                name,
                own.Count(item => item.Severity == Severity.Critical),
                own.Count(item => item.Severity == Severity.Adaptation),
                own.Count(item => item.Severity == Severity.Warning),
                own.Count(item => item.Severity == Severity.Unchecked)));
        }

        return summaries
            .OrderByDescending(item => item.Critical)
            .ThenByDescending(item => item.Adaptation)
            .ThenByDescending(item => item.Warning)
            .ThenByDescending(item => item.Unchecked)
            .ThenBy(item => item.Extension, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AnalyzeHooks(
        string extensionName,
        MetadataObject extensionObject,
        MetadataObject baseObject,
        List<ConfigChange> objectChanges,
        ConfigurationDump before,
        ConfigurationDump after,
        List<ImpactFinding> findings)
    {
        var objectRemoved = objectChanges.Any(change =>
            change.Kind == ChangeKind.Removed && change.Method == null && change.MemberName == null);

        foreach (var module in extensionObject.Modules)
        {
            foreach (var method in module.Methods)
            {
                foreach (var hook in method.Hooks)
                {
                    if (objectRemoved)
                    {
                        findings.Add(Finding(
                            extensionName,
                            baseObject.FullName,
                            module.RelativePath,
                            hook.TargetMethod,
                            Describe(hook, method),
                            Severity.Critical,
                            "Объект, в который вмешивается расширение, удалён",
                            ChangeKind.Removed));
                        continue;
                    }

                    var change = objectChanges.FirstOrDefault(item =>
                        item.Method != null
                        && item.Method.Equals(hook.TargetMethod, StringComparison.OrdinalIgnoreCase)
                        && (item.Module == null || item.Module.Equals(module.RelativePath, StringComparison.OrdinalIgnoreCase))
                        && item.Kind is ChangeKind.Removed or ChangeKind.Module);

                    if (change == null)
                        continue;

                    var anchorIntact = ResolveAnchorIntact(change, method, hook, baseObject, before, after);
                    var severity = SeverityForHook(change, method, hook, anchorIntact);
                    if (severity == null)
                        continue;

                    findings.Add(Finding(
                        extensionName,
                        baseObject.FullName,
                        change.Module ?? module.RelativePath,
                        hook.TargetMethod,
                        Describe(hook, method),
                        severity.Value,
                        ReasonForHook(change, hook, severity.Value, anchorIntact),
                        change.Kind));
                }
            }
        }
    }

    private static bool? ResolveAnchorIntact(
        ConfigChange change,
        BslMethod extensionMethod,
        MethodHook hook,
        MetadataObject baseObject,
        ConfigurationDump before,
        ConfigurationDump after)
    {
        var relevant = hook.Kind == HookKind.ChangeAndControl || extensionMethod.HasInsert || extensionMethod.HasDelete;
        if (!relevant || change.ModulePart != ModuleChangePart.Body)
            return null;

        var beforeMethod = FindBaseMethod(before, baseObject, change.Module, hook.TargetMethod);
        var afterMethod = FindBaseMethod(after, baseObject, change.Module, hook.TargetMethod);
        return AnchorAnalysis.AnchorIntact(
            extensionMethod.BodyText,
            beforeMethod?.BodyNormalized,
            afterMethod?.BodyNormalized);
    }

    private static BslMethod? FindBaseMethod(
        ConfigurationDump dump,
        MetadataObject baseObject,
        string? modulePath,
        string methodName)
    {
        var owner = FindOwner(dump, baseObject);
        if (owner == null)
            return null;

        var modules = modulePath == null
            ? owner.Modules
            : owner.Modules
                .Where(item => item.RelativePath.Equals(modulePath, StringComparison.OrdinalIgnoreCase))
                .Concat(owner.Modules.Where(item => !item.RelativePath.Equals(modulePath, StringComparison.OrdinalIgnoreCase)));

        foreach (var module in modules)
        {
            var method = module.Methods.FirstOrDefault(item =>
                item.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase));
            if (method != null)
                return method;
        }

        return null;
    }

    private static void AnalyzeCalls(
        ConfigurationDump extension,
        ConfigurationDump before,
        ConfigurationDump after,
        IReadOnlyDictionary<string, string> commonModules,
        IReadOnlyList<ConfigChange> changes,
        List<ImpactFinding> findings)
    {
        var direct = DirectCalls(extension, before, after, commonModules);

        foreach (var (baseObject, calledMethod) in direct)
        {
            if (AlreadyCovered(findings, extension.Header.Name, baseObject, calledMethod))
                continue;

            var change = ChangeOfMethod(changes, baseObject, calledMethod);
            if (change == null)
                continue;

            var signature = change.Kind == ChangeKind.Removed
                || change.ModulePart is ModuleChangePart.Signature or ModuleChangePart.Both;
            findings.Add(Finding(
                extension.Header.Name,
                baseObject.FullName,
                change.Module,
                calledMethod,
                "вызов",
                signature ? Severity.Critical : Severity.Warning,
                signature
                    ? "Расширение вызывает метод, который удалён или сменил сигнатуру"
                    : "Расширение вызывает метод, тело которого изменилось",
                change.Kind));
        }

        AnalyzeCallChain(extension, before, after, commonModules, changes, direct, findings);
    }

    /// <summary>
    /// Один шаг вглубь: расширение вызывает метод общего модуля, а тот в новой
    /// версии вызывает другой изменившийся метод. Строка добавляется только если
    /// цепочка восстановлена однозначно.
    /// </summary>
    private static void AnalyzeCallChain(
        ConfigurationDump extension,
        ConfigurationDump before,
        ConfigurationDump after,
        IReadOnlyDictionary<string, string> commonModules,
        IReadOnlyList<ConfigChange> changes,
        List<(MetadataObject Object, string Method)> direct,
        List<ImpactFinding> findings)
    {
        var directKeys = new HashSet<string>(
            direct.Select(item => CallKey(item.Object, item.Method)),
            StringComparer.OrdinalIgnoreCase);
        var chained = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (baseObject, calledMethod) in direct)
        {
            var currentObject = FindCommonModule(after, baseObject.Name);
            if (currentObject == null)
                continue;

            var body = UniqueMethod(currentObject, calledMethod);
            if (body == null)
                continue;
            if (BslText.ContainsDynamicCall(body.BodyText))
                continue;

            foreach (var (nextModule, nextMethod) in BslText.ReadCalls(body.BodyText, commonModules))
            {
                var nextObject = FindCommonModule(after, nextModule) ?? FindCommonModule(before, nextModule);
                if (nextObject == null)
                    continue;

                var key = CallKey(nextObject, nextMethod);
                if (directKeys.Contains(key) || !chained.Add(key))
                    continue;
                if (AlreadyCovered(findings, extension.Header.Name, nextObject, nextMethod))
                    continue;

                var change = ChangeOfMethod(changes, nextObject, nextMethod);
                if (change == null)
                    continue;

                findings.Add(Finding(
                    extension.Header.Name,
                    nextObject.FullName,
                    change.Module,
                    nextMethod,
                    "цепочка вызовов",
                    Severity.Warning,
                    $"Расширение вызывает {baseObject.Name}.{calledMethod}, а этот метод в новой версии "
                    + $"обращается к изменившемуся {nextObject.Name}.{nextMethod}",
                    change.Kind));
            }
        }
    }

    private static List<(MetadataObject Object, string Method)> DirectCalls(
        ConfigurationDump extension,
        ConfigurationDump before,
        ConfigurationDump after,
        IReadOnlyDictionary<string, string> commonModules)
    {
        var calls = new List<(MetadataObject Object, string Method)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var extensionObject in extension.Objects)
        {
            foreach (var module in extensionObject.Modules)
            {
                foreach (var (calledModule, calledMethod) in BslText.ReadCalls(module.Source, commonModules))
                {
                    var baseObject = FindCommonModule(after, calledModule) ?? FindCommonModule(before, calledModule);
                    if (baseObject == null)
                        continue;
                    if (!seen.Add(CallKey(baseObject, calledMethod)))
                        continue;
                    calls.Add((baseObject, calledMethod));
                }
            }
        }

        return calls;
    }

    /// <summary>Метод считается разобранным, только если имя однозначно в пределах объекта.</summary>
    private static BslMethod? UniqueMethod(MetadataObject owner, string methodName)
    {
        BslMethod? found = null;
        foreach (var module in owner.Modules)
        {
            foreach (var method in module.Methods)
            {
                if (!method.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (found != null)
                    return null;
                found = method;
            }
        }

        return found;
    }

    private static MetadataObject? FindCommonModule(ConfigurationDump dump, string name) =>
        dump.Objects.FirstOrDefault(item =>
            item.Type.Equals("CommonModule", StringComparison.OrdinalIgnoreCase)
            && item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static ConfigChange? ChangeOfMethod(
        IReadOnlyList<ConfigChange> changes,
        MetadataObject owner,
        string methodName) =>
        changes.FirstOrDefault(item =>
            SameObject(item, owner)
            && item.Method != null
            && item.Method.Equals(methodName, StringComparison.OrdinalIgnoreCase)
            && item.Kind is ChangeKind.Removed or ChangeKind.Module);

    private static bool AlreadyCovered(
        List<ImpactFinding> findings,
        string extensionName,
        MetadataObject owner,
        string methodName) =>
        findings.Any(item =>
            item.Extension == extensionName
            && item.Method != null
            && item.Method.Equals(methodName, StringComparison.OrdinalIgnoreCase)
            && item.ObjectName.Equals(owner.FullName, StringComparison.OrdinalIgnoreCase));

    private static string CallKey(MetadataObject owner, string methodName) =>
        owner.FullName + "\n" + methodName;

    private static void AnalyzeMembers(
        string extensionName,
        MetadataObject extensionObject,
        MetadataObject baseObject,
        List<ConfigChange> objectChanges,
        ConfigurationDump before,
        ConfigurationDump after,
        List<ImpactFinding> findings)
    {
        foreach (var change in objectChanges)
        {
            if (change.MemberName == null || change.MemberKind == null)
                continue;
            var borrowed = extensionObject.Members.Any(member =>
                member.Kind.Equals(change.MemberKind, StringComparison.OrdinalIgnoreCase)
                && member.Name.Equals(change.MemberName, StringComparison.OrdinalIgnoreCase));
            if (!borrowed)
                continue;

            if (change.MemberDelta == MemberDelta.Removed)
            {
                findings.Add(Finding(
                    extensionName,
                    baseObject.FullName,
                    null,
                    null,
                    "заимствование",
                    Severity.Critical,
                    change.Summary,
                    change.Kind));
            }
            else if (change.MemberDelta == MemberDelta.Modified && !PresentationOnly(change, baseObject, before, after))
            {
                findings.Add(Finding(
                    extensionName,
                    baseObject.FullName,
                    null,
                    null,
                    "заимствование",
                    Severity.Warning,
                    change.Summary,
                    change.Kind));
            }
        }
    }

    /// <summary>
    /// Правка считается косметической, только если значимый отпечаток заимствованного
    /// реквизита совпал до и после: тип, длина, состав и прочие свойства остались теми же,
    /// разошлись лишь синоним, комментарий или подсказка. Если сравнить не с чем,
    /// правка считается содержательной.
    /// </summary>
    private static bool PresentationOnly(
        ConfigChange change,
        MetadataObject baseObject,
        ConfigurationDump before,
        ConfigurationDump after)
    {
        var previous = FindMember(before, baseObject, change.MemberKind!, change.MemberName!);
        var current = FindMember(after, baseObject, change.MemberKind!, change.MemberName!);
        if (previous == null || current == null)
            return false;
        if (previous.EssentialFingerprint.Length == 0 || current.EssentialFingerprint.Length == 0)
            return false;

        return string.Equals(previous.EssentialFingerprint, current.EssentialFingerprint, StringComparison.Ordinal)
            && !string.Equals(previous.Fingerprint, current.Fingerprint, StringComparison.Ordinal);
    }

    private static ObjectMember? FindMember(
        ConfigurationDump dump,
        MetadataObject baseObject,
        string memberKind,
        string memberName)
    {
        var owner = FindOwner(dump, baseObject);
        return owner?.Members.FirstOrDefault(member =>
            member.Kind.Equals(memberKind, StringComparison.OrdinalIgnoreCase)
            && member.Name.Equals(memberName, StringComparison.OrdinalIgnoreCase));
    }

    private static MetadataObject? FindOwner(ConfigurationDump dump, MetadataObject baseObject) =>
        dump.Objects.FirstOrDefault(item =>
            (baseObject.Uuid.Length > 0 && item.Uuid.Equals(baseObject.Uuid, StringComparison.OrdinalIgnoreCase))
            || item.FullName.Equals(baseObject.FullName, StringComparison.OrdinalIgnoreCase));

    private static void AnalyzeDynamic(
        string extensionName,
        MetadataObject extensionObject,
        MetadataObject baseObject,
        List<ImpactFinding> findings)
    {
        foreach (var module in extensionObject.Modules)
        {
            if (!BslText.ContainsDynamicCall(module.Source))
                continue;
            findings.Add(Finding(
                extensionName,
                baseObject.FullName,
                module.RelativePath,
                null,
                "динамический вызов",
                Severity.Unchecked,
                "В модуле есть Выполнить или Вычислить, автоматического вывода недостаточно",
                null));
        }
    }

    /// <summary>
    /// Удаление метода и смена сигнатуры ломают перехват при любом виде крючка.
    /// Изменение одного тела разбирается по виду крючка: &amp;Перед зависит только от
    /// входных параметров, поэтому новое тело типового метода на него не влияет;
    /// &amp;После видит результат и побочные эффекты; &amp;Вместо важен лишь тогда, когда
    /// обработчик возвращает управление типовому коду через ПродолжитьВызов.
    /// </summary>
    private static Severity? SeverityForHook(ConfigChange change, BslMethod method, MethodHook hook, bool? anchorIntact)
    {
        if (change.Kind == ChangeKind.Removed || change.ModulePart is ModuleChangePart.Signature or ModuleChangePart.Both)
            return Severity.Critical;

        if (change.ModulePart != ModuleChangePart.Body)
            return null;

        if (hook.Kind == HookKind.ChangeAndControl || method.HasInsert || method.HasDelete)
            return anchorIntact == true ? Severity.Warning : Severity.Adaptation;
        if (hook.Kind == HookKind.After)
            return Severity.Warning;
        if (hook.Kind == HookKind.Instead && method.CallsProceed)
            return Severity.Warning;
        return null;
    }

    private static string ReasonForHook(ConfigChange change, MethodHook hook, Severity severity, bool? anchorIntact)
    {
        if (change.Kind == ChangeKind.Removed)
            return "Метод, в который вмешивается расширение, удалён";
        if (severity == Severity.Critical)
            return "Сигнатура метода, в который вмешивается расширение, изменилась";
        if (severity == Severity.Adaptation)
        {
            return anchorIntact == false
                ? "Тело метода изменилось в строках-якорях вставки: обновление задело именно то место, куда вмешивается расширение"
                : "Тело метода изменилось, а расширение правит его вставкой или изменением и контролем";
        }

        if (anchorIntact == true)
            return "Тело метода изменилось вне строк-якорей вставки: они сохранились, риск ниже, но проверьте вручную";
        return hook.Kind switch
        {
            HookKind.After => "Тело метода изменилось, а обработчик после него зависит от результата и побочных действий типового кода",
            HookKind.Instead => "Тело метода изменилось, а обработчик вместо него возвращает управление типовому коду через ПродолжитьВызов",
            _ => "Тело метода изменилось",
        };
    }

    private static string Describe(MethodHook hook, BslMethod method)
    {
        var label = hook.Kind switch
        {
            HookKind.Before => "Перед",
            HookKind.After => "После",
            HookKind.Instead => "Вместо",
            _ => "ИзменениеИКонтроль",
        };
        if (method.HasInsert)
            label += ", вставка";
        if (method.HasDelete)
            label += ", удаление";
        return label;
    }

    private static MetadataObject? ResolveBase(
        MetadataObject extensionObject,
        ConfigurationDump before,
        ConfigurationDump after)
    {
        if (!string.IsNullOrWhiteSpace(extensionObject.ExtendedConfigurationObject))
        {
            var linked = FindByUuid(after, extensionObject.ExtendedConfigurationObject)
                ?? FindByUuid(before, extensionObject.ExtendedConfigurationObject);
            if (linked != null)
                return linked;
        }

        return after.Objects.FirstOrDefault(item => SameTypeAndName(item, extensionObject))
            ?? before.Objects.FirstOrDefault(item => SameTypeAndName(item, extensionObject));
    }

    private static MetadataObject? FindByUuid(ConfigurationDump dump, string uuid) =>
        dump.Objects.FirstOrDefault(item => item.Uuid.Equals(uuid, StringComparison.OrdinalIgnoreCase));

    private static bool SameTypeAndName(MetadataObject left, MetadataObject right) =>
        left.Type.Equals(right.Type, StringComparison.OrdinalIgnoreCase)
        && left.Name.Equals(right.Name, StringComparison.OrdinalIgnoreCase);

    private static bool SameObject(ConfigChange change, MetadataObject baseObject)
    {
        if (!string.IsNullOrWhiteSpace(change.ObjectUuid)
            && change.ObjectUuid.Equals(baseObject.Uuid, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return change.ObjectName.Equals(baseObject.FullName, StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, string> CommonModuleNames(ConfigurationDump before, ConfigurationDump after)
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in before.Objects.Concat(after.Objects))
        {
            if (!item.Type.Equals("CommonModule", StringComparison.OrdinalIgnoreCase))
                continue;
            names.TryAdd(item.Name, item.Name);
        }

        return names;
    }

    private static ImpactFinding Finding(
        string extension,
        string objectName,
        string? module,
        string? method,
        string intervention,
        Severity severity,
        string reason,
        ChangeKind? kind) =>
        new(extension, objectName, module, method, intervention, severity, reason, kind);
}
