namespace TypovayaUp.Launcher;

static class LauncherPaths
{
    /// <summary>Корень каталога typovaya-up (OneScript CLI, соседний репозиторий/папка).</summary>
    public static string ResolveTypovayaUpRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            var sibling = Path.Combine(dir, "typovaya-up");
            if (File.Exists(Path.Combine(sibling, "src", "Главный.os")))
                return Path.GetFullPath(sibling);
            if (Directory.Exists(Path.Combine(dir, "src")) &&
                File.Exists(Path.Combine(dir, "src", "Главный.os")))
                return dir;
            var parent = Directory.GetParent(dir);
            if (parent is null)
                break;
            dir = parent.FullName;
        }
        var fromGuiBin = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "typovaya-up"));
        if (File.Exists(Path.Combine(fromGuiBin, "src", "Главный.os")))
            return fromGuiBin;
        return Path.Combine(Environment.CurrentDirectory, "typovaya-up");
    }

    /// <summary>Корень typovaya-up-gui (дистрибутивы 1С и пр.).</summary>
    public static string ResolveGuiRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            if (File.Exists(Path.Combine(dir, "TypovayaUp.Gui.sln")) ||
                Directory.Exists(Path.Combine(dir, "distributions", "1c")))
                return dir;
            var parent = Directory.GetParent(dir);
            if (parent is null)
                break;
            dir = parent.FullName;
        }
        var dev = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        if (Directory.Exists(Path.Combine(dev, "distributions", "1c")))
            return dev;
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }
}
