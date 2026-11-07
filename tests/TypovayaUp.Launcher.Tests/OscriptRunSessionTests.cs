using TypovayaUp.Launcher;
using Xunit;

namespace TypovayaUp.Launcher.Tests;

public sealed class OscriptRunSessionTests
{
    static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    static CompareLaunchRequest DummyRequest(string executable) => new()
    {
        OscriptExecutable = executable,
        MainScriptPath = Path.Combine(AppContext.BaseDirectory, "unused.os"),
        SourceDumpPath = @"C:\src",
        VendorDumpPath = @"C:\vendor",
        OutputDirectory = @"C:\out",
    };

    [Fact]
    public async Task StartAsync_MissingExecutable_FailsBeforeRunning()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "no_oscript.exe");
        await using var session = new OscriptRunSession();
        await Assert.ThrowsAnyAsync<Exception>(
            () => session.StartAsync(DummyRequest(missing), CancellationToken.None));
    }

    [Fact]
    public async Task StartAsync_NonZeroExit_MarksFailed()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var exe = FixturePath("exit7.cmd");
        Assert.True(File.Exists(exe), $"fixture missing: {exe}");

        await using var session = new OscriptRunSession();
        await session.StartAsync(DummyRequest(exe), CancellationToken.None);

        Assert.Equal(RunCompletionState.Failed, session.State);
        Assert.Equal(7, session.ExitCode);
        Assert.False(session.WasCancelled);
    }

    [Fact]
    public async Task CancelAsync_LongRunner_MarksCancelled()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var exe = FixturePath("sleepy.cmd");
        Assert.True(File.Exists(exe), $"fixture missing: {exe}");

        await using var session = new OscriptRunSession();
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(400);
        await session.StartAsync(DummyRequest(exe), cts.Token);

        Assert.Equal(RunCompletionState.Cancelled, session.State);
        Assert.True(session.WasCancelled);
    }
}
