namespace TypovayaUp.Launcher;

static class LauncherPaths
{
    /// <summary>Корень каталога typovaya-up (родитель launcher).</summary>
    public static string ResolveTypovayaUpRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 6; i++)
        {
            var candidate = dir;
            if (Directory.Exists(Path.Combine(candidate, "src")) &&
                File.Exists(Path.Combine(candidate, "src", "Главный.os")))
                return candidate;
            var parent = Directory.GetParent(dir);
            if (parent is null)
                break;
            dir = parent.FullName;
        }
        // Разработка: launcher/bin/Debug/... → typovaya-up
        var dev = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        if (File.Exists(Path.Combine(dev, "src", "Главный.os")))
            return dev;
        return Path.Combine(Environment.CurrentDirectory, "typovaya-up");
    }
}
