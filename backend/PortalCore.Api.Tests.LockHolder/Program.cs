using PortalCore.Api.Backup;

// Test helper: a separate process that holds the Portal Core maintenance lock the way the server does.
// Usage: LockHolder <shared|exclusive> <database file> [exit-after-ready|crash]
// It prints READY once the lock is held (or BUSY and exit code 3 when it cannot be taken), then waits
// until its standard input is closed. "crash" ends the process abruptly without releasing anything itself.
if (args.Length < 2)
{
    Console.Error.WriteLine("usage: LockHolder <shared|exclusive> <database file>");
    return 64;
}

MaintenanceLock held;
try
{
    held = args[0] == "exclusive" ? MaintenanceLock.AcquireExclusive(args[1]) : MaintenanceLock.AcquireShared(args[1]);
}
catch (BackupException ex)
{
    Console.WriteLine("BUSY " + ex.Message);
    return 3;
}

Console.WriteLine("READY");
Console.Out.Flush();

if (args.Length > 2 && args[2] == "crash")
{
    // Abnormal termination: no Dispose, no finally blocks.
    Environment.FailFast("simulated crash");
}

Console.In.ReadToEnd();
held.Dispose();
return 0;
