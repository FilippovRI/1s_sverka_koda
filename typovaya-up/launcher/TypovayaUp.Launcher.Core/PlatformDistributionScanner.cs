namespace TypovayaUp.Launcher;

/// <summary>Поиск MSI/MST в пользовательской папке дистрибутива (без привязки к версии/GUID).</summary>
public static class PlatformDistributionScanner
{
    public sealed record ScanResult(
        string Folder,
        bool FolderExists,
        IReadOnlyList<string> MsiFiles,
        IReadOnlyList<string> MstFiles,
        string? RecommendedMsi,
        string? RecommendedMst,
        IReadOnlyList<string> Notes);

    public static ScanResult Scan(string? folder)
    {
        var notes = new List<string>();
        if (string.IsNullOrWhiteSpace(folder))
        {
            return new("", false, [], [], null, null, ["Папка дистрибутива не указана."]);
        }

        if (!Directory.Exists(folder))
        {
            notes.Add("Папка не существует. Создайте её и скопируйте дистрибутив (см. distributions/1c/README.md).");
            return new(folder, false, [], [], null, null, notes);
        }

        var msi = CollectFiles(folder, "*.msi");
        var mst = CollectFiles(folder, "*.mst");

        if (msi.Count == 0)
        {
            notes.Add("Не найден ни один .msi. Скопируйте полный дистрибутив платформы 1С в эту папку.");
            return new(folder, true, msi, mst, null, null, notes);
        }

        var recommendedMsi = PickMsi(msi);
        string? recommendedMst = null;
        if (mst.Count > 0)
            recommendedMst = PickMst(mst);

        if (mst.Count == 0)
            notes.Add("Файл .mst не найден — установка возможна без TRANSFORMS (язык зависит от дистрибутива).");
        else if (recommendedMst is not null)
            notes.Add($"Для TRANSFORMS предлагается: {Path.GetFileName(recommendedMst)}");

        notes.Add($"Для установки CSServer предлагается MSI: {Path.GetFileName(recommendedMsi)}");
        notes.Add("Установка только по кнопке в GUI; потребуется UAC (Run as administrator).");

        return new(folder, true, msi, mst, recommendedMsi, recommendedMst, notes);
    }

    public static bool IsInstallable(ScanResult scan) =>
        scan.FolderExists && scan.RecommendedMsi is not null && File.Exists(scan.RecommendedMsi);

    static List<string> CollectFiles(string root, string pattern)
    {
        var list = new List<string>();
        try
        {
            list.AddRange(Directory.EnumerateFiles(root, pattern, SearchOption.TopDirectoryOnly));
            foreach (var sub in Directory.EnumerateDirectories(root))
            {
                list.AddRange(Directory.EnumerateFiles(sub, pattern, SearchOption.TopDirectoryOnly));
            }
        }
        catch
        {
            // ignore IO errors — отразим в UI
        }
        return list.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
    }

    static string PickMsi(IReadOnlyList<string> files)
    {
        var ranked = files
            .Select(f => (Path: f, Score: ScoreMsi(Path.GetFileName(f))))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return ranked[0].Path;
    }

    static int ScoreMsi(string name)
    {
        var n = name.ToLowerInvariant();
        var score = 0;
        if (n.Contains("1centerprise")) score += 10;
        if (n.Contains("x86-64") || n.Contains("x64")) score += 5;
        if (n.Contains("8")) score += 2;
        return score;
    }

    static string PickMst(IReadOnlyList<string> files)
    {
        var ranked = files
            .Select(f => (Path: f, Score: ScoreMst(Path.GetFileName(f))))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return ranked[0].Path;
    }

    static int ScoreMst(string name)
    {
        var n = name.ToLowerInvariant();
        if (n == "1049.mst") return 20;
        if (n.Contains("1049")) return 15;
        if (n.Contains("rus") || n.Contains("ru")) return 10;
        return 1;
    }
}
