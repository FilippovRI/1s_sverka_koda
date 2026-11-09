namespace TypovayaUp.Launcher;

public static class PlatformInstallRunner
{
    /// <summary>Запуск msiexec с повышением прав (UAC). Установка не ждётся — проверьте ibcmd вручную.</summary>
    public static void StartElevated(PlatformCsserverInstall.InstallPlan plan)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Установка платформы только на Windows.");

        var args = PlatformCsserverInstall.FormatMsiexecArguments(plan.MsiexecArgumentList);
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = Environment.SystemDirectory + "\\msiexec.exe",
            Arguments = args,
            UseShellExecute = true,
            Verb = "runas",
        };
        System.Diagnostics.Process.Start(psi);
    }
}
