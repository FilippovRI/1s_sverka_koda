namespace TypovayaUp.Launcher;

/// <summary>Параметры запуска src/Главный.os (без shell).</summary>
public sealed class CompareLaunchRequest
{
    public required string OscriptExecutable { get; init; }
    public required string MainScriptPath { get; init; }
    public required string SourceDumpPath { get; init; }
    public required string VendorDumpPath { get; init; }
    public string? SettingsXmlPath { get; init; }
    public required string OutputDirectory { get; init; }
    public string ExtensionName { get; init; } = "УП_Доработки";
    public string NamePrefix { get; init; } = "УП_";
    public bool AnalyzeForms { get; init; }
    public string WorkingDirectory { get; init; } = "";

    /// <summary>Аргументы процесса: oscript.exe + script + флаги (каждое значение — отдельный argv).</summary>
    public IReadOnlyList<string> BuildArgumentList()
    {
        var args = new List<string> { MainScriptPath };
        AddPair(args, "--исходная", SourceDumpPath);
        AddPair(args, "--поставщик", VendorDumpPath);
        if (!string.IsNullOrWhiteSpace(SettingsXmlPath))
            AddPair(args, "--настройки", SettingsXmlPath);
        AddPair(args, "--выход", OutputDirectory);
        if (!string.IsNullOrWhiteSpace(ExtensionName))
            AddPair(args, "--расширение", ExtensionName);
        if (!string.IsNullOrWhiteSpace(NamePrefix))
            AddPair(args, "--префикс", NamePrefix);
        if (AnalyzeForms)
            args.Add("--анализ-форм");
        return args;
    }

    static void AddPair(List<string> args, string flag, string value)
    {
        args.Add(flag);
        args.Add(value);
    }
}
