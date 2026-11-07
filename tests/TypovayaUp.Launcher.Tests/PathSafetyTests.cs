using TypovayaUp.Launcher;
using Xunit;

namespace TypovayaUp.Launcher.Tests;

public class PathSafetyTests
{
    [Fact]
    public void OutputInsideSource_IsRejected()
    {
        var err = PathSafety.ValidateOutputVersusSources(
            Path.GetFullPath("/tmp/out"),
            Path.GetFullPath("/tmp"),
            Path.GetFullPath("/other"));
        Assert.NotNull(err);
    }
}
