namespace TypovayaUp.Launcher;

public static class PathSafety
{
    public const string ConfigurationFileName = "Configuration.xml";

    /// <summary>Выход не должен совпадать с выгрузками и не лежать внутри них (как в Главный.os).</summary>
    public static string? ValidateOutputVersusSources(string outputPath, string sourcePath, string vendorPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            return "Не указана папка результата.";

        var outFull = NormalizeDir(outputPath);
        foreach (var (label, input) in new[] { ("исходной", sourcePath), ("поставщика", vendorPath) })
        {
            if (string.IsNullOrWhiteSpace(input))
                continue;
            var inFull = NormalizeDir(input);
            if (PathsEqual(outFull, inFull))
                return $"Папка результата совпадает с выгрузкой {label}.";
            if (IsUnder(outFull, inFull))
                return $"Папка результата не может лежать внутри выгрузки {label}.";
            if (IsUnder(inFull, outFull))
                return $"Выгрузка {label} не должна оказаться внутри папки результата (защита исходников).";
        }
        return null;
    }

    public static string? ValidateDumpFolder(string? path, string label)
    {
        if (string.IsNullOrWhiteSpace(path))
            return $"Не указана папка выгрузки ({label}).";
        if (!Directory.Exists(path))
            return $"Папка не найдена ({label}): {path}";
        var cfg = Path.Combine(path, ConfigurationFileName);
        if (!File.Exists(cfg))
            return $"Нет {ConfigurationFileName} в {path} ({label}).";
        return null;
    }

    public static string? ValidateOptionalSettingsFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        if (!File.Exists(path))
            return $"Файл настроек не найден: {path}";
        return null;
    }

    public static string NormalizeDir(string path)
    {
        var full = Path.GetFullPath(path.Trim());
        if (!full.EndsWith(Path.DirectorySeparatorChar))
            full += Path.DirectorySeparatorChar;
        return full;
    }

    public static bool PathsEqual(string a, string b) =>
        string.Equals(a.TrimEnd(Path.DirectorySeparatorChar), b.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);

    public static bool IsUnder(string childDir, string parentDir) =>
        childDir.StartsWith(parentDir, StringComparison.OrdinalIgnoreCase);
}
