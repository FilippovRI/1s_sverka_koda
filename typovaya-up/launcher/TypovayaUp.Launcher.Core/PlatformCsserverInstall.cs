namespace TypovayaUp.Launcher;

/// <summary>Параметры тихой установки только CSServer (+ языковой пакет) без службы кластера.</summary>
public static class PlatformCsserverInstall
{
    public const string AddLocal = "CSServer,CSServer_RU";

    public sealed record InstallPlan(
        string MsiPath,
        string? TransformPath,
        string LogFilePath,
        IReadOnlyList<string> MsiexecArgumentList);

    public static InstallPlan BuildPlan(PlatformDistributionScanner.ScanResult scan, string? logDirectory)
    {
        if (!PlatformDistributionScanner.IsInstallable(scan))
            throw new InvalidOperationException("Дистрибутив не готов к установке.");

        logDirectory ??= Path.Combine(Path.GetTempPath(), "typovaya-up-msi-logs");
        Directory.CreateDirectory(logDirectory);
        var logFile = Path.Combine(logDirectory, $"1c-csserver-{DateTime.Now:yyyyMMdd-HHmmss}.log");

        var args = new List<string>
        {
            "/i", scan.RecommendedMsi!,
        };
        if (!string.IsNullOrEmpty(scan.RecommendedMst) && File.Exists(scan.RecommendedMst))
        {
            args.Add("TRANSFORMS=" + scan.RecommendedMst);
        }
        args.AddRange([
            $"ADDLOCAL={AddLocal}",
            "INSTALLSRVRASSRVC=0",
            "MSIRESTARTMANAGERCONTROL=Disable",
            "REBOOT=ReallySuppress",
            "/qn",
            "/norestart",
            "/l*v",
            logFile,
        ]);

        return new(scan.RecommendedMsi!, scan.RecommendedMst, logFile, args);
    }

    /// <summary>Формирует одну строку для msiexec (кавычки только вокруг путей с пробелами).</summary>
    public static string FormatMsiexecArguments(IReadOnlyList<string> argumentList)
    {
        return string.Join(" ", argumentList.Select(QuoteIfNeeded));
    }

    static string QuoteIfNeeded(string value)
    {
        if (value.Contains(' ') || value.Contains('\t'))
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        return value;
    }

    public static bool IsFailureExitCode(int? exitCode) =>
        exitCode is 1603 or 1730 || (exitCode is int c && c != 0 && c != 3010); // 3010 = reboot required

    public static IReadOnlyList<string> FindIbcmdCandidates()
    {
        var list = new List<string>();
        if (!OperatingSystem.IsWindows())
            return list;

        void AddIfExists(string path)
        {
            if (File.Exists(path))
                list.Add(path);
        }

        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        AddIfExists(Path.Combine(pf, "1cv8", "common", "ibcmd.exe"));
        AddIfExists(Path.Combine(pf, "1cv8", "x86_64", "ibcmd.exe"));
        var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        AddIfExists(Path.Combine(pf86, "1cv8", "common", "ibcmd.exe"));

        var roots = new[] { pf, pf86 };
        foreach (var root in roots)
        {
            var v8 = Path.Combine(root, "1cv8");
            if (!Directory.Exists(v8))
                continue;
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(v8))
                {
                    AddIfExists(Path.Combine(dir, "bin", "ibcmd.exe"));
                    AddIfExists(Path.Combine(dir, "ibcmd.exe"));
                }
            }
            catch
            {
                // ignore
            }
        }
        return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
