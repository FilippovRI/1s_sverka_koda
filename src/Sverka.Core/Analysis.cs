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
