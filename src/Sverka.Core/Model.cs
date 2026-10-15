namespace Sverka;

public enum ChangeKind
{
    Added,
    Removed,
    Renamed,
    Properties,
    Composition,
    Module,
}

public enum MemberDelta
{
    None,
    Added,
    Removed,
    Modified,
}

public enum ModuleChangePart
{
    Body,
    Signature,
    Both,
}

public enum Severity
{
    Critical,
    Adaptation,
    Warning,
    Unchecked,
}

public enum HookKind
{
    Before,
    After,
    Instead,
    ChangeAndControl,
}

public sealed record ConfigurationHeader(
    string Uuid,
    string Name,
    string Version,
    string CompatibilityMode,
    string Vendor);

/// <summary>
/// Fingerprint — отпечаток всех свойств, по нему строится отчёт по конфигурации.
/// EssentialFingerprint — тот же отпечаток без представленческих узлов (синоним,
/// комментарий, подсказка). Если отличается только Fingerprint, правка косметическая.
/// </summary>
public sealed record ObjectMember(string Kind, string Name, string Fingerprint, string EssentialFingerprint);

public sealed record MethodHook(HookKind Kind, string TargetMethod);

public sealed record BslMethod(
    string Name,
    bool IsFunction,
    string SignatureNormalized,
    string BodyNormalized,
    IReadOnlyList<MethodHook> Hooks,
    bool HasInsert,
    bool HasDelete,
    bool CallsProceed,
    string BodyText);

public sealed record CodeModule(
    string RelativePath,
    string Source,
    IReadOnlyList<BslMethod> Methods);

public sealed record MetadataObject(
    string Uuid,
    string Type,
    string Name,
    string PropertiesFingerprint,
    string? ExtendedConfigurationObject,
    IReadOnlyList<ObjectMember> Members,
    IReadOnlyList<CodeModule> Modules)
{
    public string FullName => $"{Type}.{Name}";
}

public sealed record ConfigurationDump(
    string RootPath,
    ConfigurationHeader Header,
    IReadOnlyList<MetadataObject> Objects);

public sealed record ConfigChange(
    string ObjectName,
    string? ObjectUuid,
    string? Module,
    string? Method,
    string? MemberKind,
    string? MemberName,
    ChangeKind Kind,
    MemberDelta MemberDelta,
    ModuleChangePart? ModulePart,
    string Summary);

public sealed record ImpactFinding(
    string Extension,
    string ObjectName,
    string? Module,
    string? Method,
    string Intervention,
    Severity Severity,
    string Reason,
    ChangeKind? ConfigChangeKind);

/// <summary>
/// Сводка по одному расширению для пакетного прогона: сколько находок каждого
/// уровня. Расширение без находок присутствует с нулями — на крупном внедрении
/// важно видеть и «чистые» расширения, а не только проблемные.
/// </summary>
public sealed record ExtensionSummary(
    string Extension,
    int Critical,
    int Adaptation,
    int Warning,
    int Unchecked)
{
    public int Total => Critical + Adaptation + Warning + Unchecked;
}

/// <summary>
/// Решение по доработке при приведении к типовой: автоматом в расширение,
/// вручную, нужна миграция данных, или пропуск (косметика).
/// </summary>
public enum TransferDecision
{
    Auto,
    Manual,
    /// <summary>Только подсветка в реестре: не генерируем файлы расширения.</summary>
    Highlight,
    DataMigration,
    Skip,
}

public enum ProposedHook
{
    None,
    Before,
    After,
    Instead,
    ChangeAndControl,
}

/// <summary>
/// Одна доработка относительно типовой выгрузки той же версии.
/// </summary>
public sealed record TransferItem(
    string ObjectName,
    string? ObjectUuid,
    string ObjectType,
    string? Module,
    string? Method,
    string? MemberKind,
    ChangeKind ChangeKind,
    TransferDecision Decision,
    string Reason,
    ProposedHook ProposedHook,
    string? HookBody,
    string? MethodHeader,
    bool IsFunction,
    string? AddedMethodSource);

public sealed record TransferPlan(
    string ExtensionName,
    string Prefix,
    ConfigurationDump Typical,
    ConfigurationDump Customized,
    IReadOnlyList<ConfigChange> Changes,
    IReadOnlyList<TransferItem> Items);

public static class ExitCodes
{
    public const int Ok = 0;
    public const int Review = 1;
    public const int Critical = 2;

    public static int FromFindings(IEnumerable<ImpactFinding> findings)
    {
        var anyCritical = false;
        var anyReview = false;
        foreach (var finding in findings)
        {
            if (finding.Severity == Severity.Critical)
                anyCritical = true;
            else if (finding.Severity is Severity.Adaptation or Severity.Warning)
                anyReview = true;
        }

        if (anyCritical)
            return Critical;
        if (anyReview)
            return Review;
        return Ok;
    }
}
