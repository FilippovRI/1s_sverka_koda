namespace Sverka;

public sealed record AnalysisResult(
    ConfigurationDump Before,
    ConfigurationDump After,
    IReadOnlyList<ConfigChange> Changes,
    IReadOnlyList<ImpactFinding> Findings,
    bool ExtensionsChecked,
    IReadOnlyList<string> Extensions,
    IReadOnlyList<ExtensionSummary> Summaries);

public static class Analysis
{
    public static AnalysisResult Run(string beforePath, string afterPath, string? extensionsPath)
    {
        var before = DumpReader.Read(beforePath);
        var after = DumpReader.Read(afterPath);
        var changes = ConfigComparer.Compare(before, after);
        if (string.IsNullOrWhiteSpace(extensionsPath))
            return new AnalysisResult(before, after, changes, [], false, [], []);

        var extensions = DumpReader.ReadExtensions(extensionsPath);
        var findings = ExtensionAnalyzer.Analyze(before, after, changes, extensions);
        var names = extensions
            .Select(extension => extension.Header.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();
        var summaries = ExtensionAnalyzer.Summarize(names, findings);
        return new AnalysisResult(before, after, changes, findings, true, names, summaries);
    }

    public static IReadOnlyList<RunDelta> CompareRuns(string previousPath, string currentPath) =>
        RunComparer.Compare(previousPath, currentPath);

    public static TransferPlan RunTransfer(
        string typicalPath,
        string customizedPath,
        string extensionName,
        string prefix,
        TransferScope? scope = null,
        bool includeRemovedMetadata = false)
    {
        var typical = DumpReader.Read(typicalPath);
        var customized = DumpReader.Read(customizedPath);
        var subsystems = SubsystemCatalog.Merge(
            SubsystemCatalog.Read(typicalPath),
            SubsystemCatalog.Read(customizedPath));
        return CustomizationFinder.Find(
            typical,
            customized,
            extensionName,
            prefix,
            scope,
            subsystems,
            includeRemovedMetadata);
    }

    public static IReadOnlyList<SubsystemInfo> LoadSubsystems(string typicalPath, string customizedPath) =>
        SubsystemCatalog.Merge(
            SubsystemCatalog.Read(typicalPath),
            SubsystemCatalog.Read(customizedPath));

    public static TransferScope BuildScope(
        IReadOnlyList<SubsystemInfo> subsystems,
        IEnumerable<string> selectedFullNames,
        bool includeDescendants,
        IEnumerable<string>? extraObjectNames = null)
    {
        var names = selectedFullNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var extras = (extraObjectNames ?? [])
            .Select(name => name.Trim())
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var objects = SubsystemCatalog.CollectObjectNames(subsystems, names, includeDescendants);
        foreach (var extra in extras)
            objects.Add(extra);
        return new TransferScope(names, objects, includeDescendants, extras);
    }

    public static void WriteTransferReports(
        string directory,
        TransferPlan plan,
        bool writeDraft,
        TransferSession? session = null)
    {
        Directory.CreateDirectory(directory);
        ReportWriter.WriteTransfer(Path.Combine(directory, "transfer.json"), plan);
        ReportWriter.WriteTransferHtml(Path.Combine(directory, "transfer.html"), plan);
        SubsystemCatalog.SaveSelection(Path.Combine(directory, "subsystems.json"), plan.Scope);
        DataMigrationWriter.Write(Path.Combine(directory, "migration"), plan);

        var activeSession = session ?? new TransferSession
        {
            ExtensionName = plan.ExtensionName,
            Prefix = plan.Prefix,
        };
        activeSession.ExtensionName = plan.ExtensionName;
        activeSession.Prefix = plan.Prefix;
        activeSession.LastDraftPath = directory;
        TransferSessionStore.Save(Path.Combine(directory, "transfer-session.json"), activeSession);

        if (!writeDraft)
            return;

        var extensionDir = Path.Combine(directory, "extension");
        var doneKeys = activeSession.Items
            .Where(entry => entry.Status == TransferWorkStatus.Done)
            .Select(entry => entry.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        // Если есть отметки «Готово» — инкремент только по ним; иначе полный авто-черновик.
        IReadOnlySet<string>? onlyKeys = doneKeys.Count > 0 ? doneKeys : null;
        ExtensionDraftWriter.Write(extensionDir, plan, onlyKeys, merge: true);
        var formsDir = Path.Combine(directory, "forms");
        FormProgrammaticWriter.Write(formsDir, plan, onlyKeys);
        var coverage = CoverageChecker.Analyze(plan, extensionDir, activeSession, formsDir);
        CoverageChecker.WriteReports(directory, coverage);
    }

    public static void WriteTransferScope(string path, TransferScope scope) =>
        SubsystemCatalog.SaveSelection(path, scope);

    public static void WriteReports(string directory, AnalysisResult result)
    {
        Directory.CreateDirectory(directory);
        ReportWriter.WriteConfig(Path.Combine(directory, "config.json"), result.Before, result.After, result.Changes);
        ReportWriter.WriteConfigHtml(Path.Combine(directory, "config.html"), result.Changes);
        if (!result.ExtensionsChecked)
            return;
        ReportWriter.WriteImpact(Path.Combine(directory, "impact.json"), result.Findings);
        ReportWriter.WriteImpactHtml(Path.Combine(directory, "impact.html"), result.Findings, result.Summaries);
        ReportWriter.WriteExtensionSummary(Path.Combine(directory, "extensions.json"), result.Summaries);
    }
}
