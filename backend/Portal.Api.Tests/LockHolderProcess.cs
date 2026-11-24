using System.Diagnostics;

namespace Portal.Api.Tests;

/// <summary>
/// Runs the Portal.Api.Tests.LockHolder helper as a separate operating-system process that holds the
/// maintenance lock, standing in for a running Navigation Harbour server (or another restore).
/// </summary>
public sealed class LockHolderProcess : IDisposable
{
    private readonly Process _process;

    private LockHolderProcess(Process process, string firstLine)
    {
        _process = process;
        FirstLine = firstLine;
    }

    /// <summary>"READY" when the lock was obtained, otherwise "BUSY ...".</summary>
    public string FirstLine { get; }

    public bool HoldsLock => FirstLine == "READY";

    public int Id => _process.Id;

    public static LockHolderProcess Start(string mode, string databaseFile, string? extra = null)
    {
        var dll = Path.Combine(AppContext.BaseDirectory, "Portal.Api.Tests.LockHolder.dll");
        var info = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        info.ArgumentList.Add(dll);
        info.ArgumentList.Add(mode);
        info.ArgumentList.Add(databaseFile);
        if (extra is not null) info.ArgumentList.Add(extra);

        var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start the lock holder process.");
        process.ErrorDataReceived += (_, _) => { };
        process.BeginErrorReadLine();

        var line = process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(60)).GetAwaiter().GetResult();
        return new LockHolderProcess(process, line ?? "");
    }

    /// <summary>Ends the process normally (it closes its lock) and waits for it to exit.</summary>
    public void ReleaseGracefully()
    {
        try { _process.StandardInput.Close(); } catch (IOException) { }
        WaitForExit();
    }

    /// <summary>Kills the process without giving it any chance to clean up.</summary>
    public void Kill()
    {
        try { _process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
        WaitForExit();
    }

    /// <summary>Waits for the process to end by itself (used for the crash mode).</summary>
    public int WaitForExit()
    {
        if (!_process.WaitForExit(30_000))
        {
            _process.Kill(entireProcessTree: true);
            _process.WaitForExit();
        }

        return _process.ExitCode;
    }

    public void Dispose()
    {
        try
        {
            if (!_process.HasExited) _process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }

        _process.Dispose();
    }
}
