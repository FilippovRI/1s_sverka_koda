using TypovayaUp.Launcher;
using Xunit;

namespace TypovayaUp.Launcher.Tests;

public class PlatformScannerTests
{
    [Fact]
    public void EmptyFolder_HasNoMsi()
    {
        var dir = Path.Combine(Path.GetTempPath(), "typovaya-scan-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            var r = PlatformDistributionScanner.Scan(dir);
            Assert.True(r.FolderExists);
            Assert.Null(r.RecommendedMsi);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Msi_Picked()
    {
        var dir = Path.Combine(Path.GetTempPath(), "typovaya-scan-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        var msi = Path.Combine(dir, "1CEnterprise 8 (x86-64).msi");
        File.WriteAllText(msi, "");
        try
        {
            var r = PlatformDistributionScanner.Scan(dir);
            Assert.Equal(msi, r.RecommendedMsi);
            var plan = PlatformCsserverInstall.BuildPlan(r, dir);
            Assert.Contains("/i", plan.MsiexecArgumentList);
        }
        finally { Directory.Delete(dir, true); }
    }
}
