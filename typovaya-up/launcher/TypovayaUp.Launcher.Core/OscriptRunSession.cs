namespace TypovayaUp.Launcher;

public enum RunCompletionState
{
    NotStarted,
    Running,
    Succeeded,
    Failed,
    Cancelled
}

public sealed class OscriptRunSession : IAsyncDisposable
{
    System.Diagnostics.Process? _process;
    readonly object _gate = new();

    public RunCompletionState State { get; private set; } = RunCompletionState.NotStarted;
    public int? ExitCode { get; private set; }
    public bool WasCancelled { get; private set; }

    public event Action<string>? OnStdout;
    public event Action<string>? OnStderr;
    public event Action<RunCompletionState, int?>? OnCompleted;

    public async Task StartAsync(CompareLaunchRequest request, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (State == RunCompletionState.Running)
                throw new InvalidOperationException("Уже выполняется.");
            State = RunCompletionState.Running;
        }

        _process = new System.Diagnostics.Process();
        _process.StartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = request.OscriptExecutable,
            WorkingDirectory = string.IsNullOrWhiteSpace(request.WorkingDirectory)
                ? Path.GetDirectoryName(request.MainScriptPath) ?? ""
                : request.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
            CreateNoWindow = true,
        };
        foreach (var arg in request.BuildArgumentList())
            _process.StartInfo.ArgumentList.Add(arg);

        _process.OutputDataReceived += (_, e) => { if (e.Data is not null) OnStdout?.Invoke(e.Data); };
        _process.ErrorDataReceived += (_, e) => { if (e.Data is not null) OnStderr?.Invoke(e.Data); };

        try
        {
            if (!_process.Start())
                throw new InvalidOperationException("Не удалось запустить oscript.");

            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            await WaitForExitWithCancelAsync(_process, cancellationToken).ConfigureAwait(false);

            lock (_gate)
            {
                ExitCode = _process.HasExited ? _process.ExitCode : -1;
                if (WasCancelled)
                {
                    State = RunCompletionState.Cancelled;
                }
                else if (ExitCode == 0)
                {
                    State = RunCompletionState.Succeeded;
                }
                else
                {
                    State = RunCompletionState.Failed;
                }
            }
        }
        catch (OperationCanceledException)
        {
            WasCancelled = true;
            await TryKillTreeAsync().ConfigureAwait(false);
            lock (_gate)
            {
                State = RunCompletionState.Cancelled;
                ExitCode = null;
            }
        }
        finally
        {
            OnCompleted?.Invoke(State, ExitCode);
        }
    }

    static async Task WaitForExitWithCancelAsync(System.Diagnostics.Process process, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        process.EnableRaisingEvents = true;
        process.Exited += (_, _) => tcs.TrySetResult();
        if (process.HasExited)
            return;
        using var reg = ct.Register(() => tcs.TrySetCanceled(ct));
        await tcs.Task.ConfigureAwait(false);
    }

    public Task CancelAsync()
    {
        WasCancelled = true;
        return TryKillTreeAsync();
    }

    async Task TryKillTreeAsync()
    {
        var p = _process;
        if (p is null || p.HasExited)
            return;
        try
        {
            if (OperatingSystem.IsWindows())
                p.Kill(entireProcessTree: true);
            else
                p.Kill(entireProcessTree: true);
        }
        catch
        {
            // best effort
        }
        await Task.Delay(200).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
    {
        _process?.Dispose();
        return ValueTask.CompletedTask;
    }
}
