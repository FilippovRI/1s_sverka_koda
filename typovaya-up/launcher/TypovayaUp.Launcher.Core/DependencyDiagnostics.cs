namespace TypovayaUp.Launcher;

public enum DependencyKind
{
    Required,
    Optional
}

public sealed record DependencyCheckResult(
    string Id,
    string Title,
    DependencyKind Kind,
    bool Ok,
    string Detail);

/// <summary>Проверка OneScript (обязательно) и опционально v8find / ibcmd.</summary>
public static class DependencyDiagnostics
{
    public static async Task<IReadOnlyList<DependencyCheckResult>> RunAsync(
        string? oscriptOverride,
        string typovayaUpRoot,
        CancellationToken cancellationToken = default)
    {
        var list = new List<DependencyCheckResult>();
        list.Add(await CheckOscriptAsync(oscriptOverride, cancellationToken).ConfigureAwait(false));
        list.Add(CheckMainScript(typovayaUpRoot));
        list.Add(await CheckV8FindAsync(oscriptOverride, cancellationToken).ConfigureAwait(false));
        list.Add(CheckIbcmdOptional());
        return list;
    }

    public static async Task<DependencyCheckResult> CheckOscriptAsync(string? oscriptOverride, CancellationToken ct)
    {
        var exe = ResolveOscriptPath(oscriptOverride);
        if (exe is null)
            return new("oscript", "OneScript (oscript)", DependencyKind.Required, false,
                "Не найден oscript.exe. Укажите путь в OSCRIPT или установите OneScript 2.x (https://oscript.io).");

        try
        {
            var (code, stdout) = await RunCaptureAsync(exe, ["-version"], ct).ConfigureAwait(false);
            if (code != 0)
                return new("oscript", "OneScript (oscript)", DependencyKind.Required, false,
                    $"oscript -version завершился с кодом {code}.");
            var ver = stdout.Trim();
            return new("oscript", "OneScript (oscript)", DependencyKind.Required, true,
                $"{exe} — версия {ver}");
        }
        catch (Exception ex)
        {
            return new("oscript", "OneScript (oscript)", DependencyKind.Required, false, ex.Message);
        }
    }

    public static DependencyCheckResult CheckMainScript(string typovayaUpRoot)
    {
        var script = Path.Combine(typovayaUpRoot, "src", "Главный.os");
        var ok = File.Exists(script);
        return new("main", "Сценарий Главный.os", DependencyKind.Required, ok,
            ok ? script : $"Не найден: {script}");
    }

    public static async Task<DependencyCheckResult> CheckV8FindAsync(string? oscriptOverride, CancellationToken ct)
    {
        var exe = ResolveOscriptPath(oscriptOverride);
        if (exe is null)
            return new("v8find", "Библиотека opm v8find", DependencyKind.Optional, false,
                "Пропущено: нет oscript для opm.");

        var opm = Path.Combine(Path.GetDirectoryName(exe)!, "opm");
        if (!OperatingSystem.IsWindows())
            opm += ".bat";
        if (!File.Exists(opm))
            return new("v8find", "Библиотека opm v8find", DependencyKind.Optional, false,
                $"Не найден opm рядом с oscript ({opm}). Для сравнения не нужен; для ibcmd — opm install v8find.");

        try
        {
            var (code, stdout) = await RunCaptureAsync(opm, ["list"], ct).ConfigureAwait(false);
            var has = stdout.Contains("v8find", StringComparison.OrdinalIgnoreCase);
            return new("v8find", "Библиотека opm v8find", DependencyKind.Optional, has,
                has ? "Пакет v8find в списке opm." : "Не установлен: opm install v8find (опционально).");
        }
        catch (Exception ex)
        {
            return new("v8find", "Библиотека opm v8find", DependencyKind.Optional, false, ex.Message);
        }
    }

    public static DependencyCheckResult CheckIbcmdOptional()
    {
        var candidates = new List<string>();
        if (OperatingSystem.IsWindows())
        {
            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            candidates.Add(Path.Combine(pf, "1cv8", "common", "ibcmd.exe"));
            candidates.Add(Path.Combine(pf, "1cv8", "x86_64", "ibcmd.exe"));
        }

        foreach (var c in candidates)
        {
            if (File.Exists(c))
                return new("ibcmd", "ibcmd (платформа 1С)", DependencyKind.Optional, true, c);
        }

        return new("ibcmd", "ibcmd (платформа 1С)", DependencyKind.Optional, false,
            "Не найден. Устанавливается с лицензированным дистрибутивом платформы (компонент CSServer). Сравнение модулей не требует ibcmd.");
    }

    public static string? ResolveOscriptPath(string? overridePath)
    {
        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
            return Path.GetFullPath(overridePath);

        var env = Environment.GetEnvironmentVariable("OSCRIPT");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
            return Path.GetFullPath(env);

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir.Trim(), OperatingSystem.IsWindows() ? "oscript.exe" : "oscript");
            if (File.Exists(candidate))
                return candidate;
        }

        if (OperatingSystem.IsWindows())
        {
            var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OneScript", "bin", "oscript.exe");
            if (File.Exists(local))
                return local;
        }
        return null;
    }

    static async Task<(int ExitCode, string StdOut)> RunCaptureAsync(string exe, IReadOnlyList<string> args, CancellationToken ct)
    {
        using var process = new System.Diagnostics.Process();
        process.StartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = exe,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var a in args)
            process.StartInfo.ArgumentList.Add(a);
        process.Start();
        var stdout = await process.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
        await process.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
        await process.WaitForExitAsync(ct).ConfigureAwait(false);
        return (process.ExitCode, stdout);
    }
}
