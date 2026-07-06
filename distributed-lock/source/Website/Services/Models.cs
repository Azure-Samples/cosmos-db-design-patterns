namespace DistributedLockWeb.Services;

public enum WorkerState
{
    Idle,
    Trying,
    Waiting,
    Holding,
    Crashed
}

/// <summary>
/// One on-screen "worker" (an independent process competing for a lock). Holds both its
/// user-controlled settings and its live runtime state. All fields are read by the UI and
/// written by the worker's background loop; the <see cref="SimulationService"/> guards mutations.
/// </summary>
public class Worker
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#38bdf8";

    // ---- Settings (user controlled) ----
    public string LockName { get; set; } = "resource-A";
    public int WorkDurationSeconds { get; set; } = 6;
    public bool RenewalEnabled { get; set; } = true;
    public bool WaitMode { get; set; }                 // false = TryAcquire (fail fast), true = AcquireLock(timeout)
    public int WaitTimeoutSeconds { get; set; } = 5;

    // ---- Live runtime state ----
    public WorkerState State { get; set; } = WorkerState.Idle;
    public long FencingToken { get; set; }
    public int TtlSeconds { get; set; }
    public DateTimeOffset? HoldStartedUtc { get; set; }
    public DateTimeOffset? WorkEndsUtc { get; set; }
    public DateTimeOffset? LeaseExpiresUtc { get; set; }
    public int RenewCount { get; set; }
    public bool Running { get; set; }
    public bool LockExpiredWhileWorking { get; set; }
    public string? Status { get; set; }

    internal CancellationTokenSource? Cts { get; set; }
    internal Task? LoopTask { get; set; }
    internal Cosmos.DistributedLock.CosmosDistributedLock? CurrentLock { get; set; }

    /// <summary>Seconds remaining until the lease would expire (for the countdown bar).</summary>
    public double LeaseSecondsRemaining =>
        LeaseExpiresUtc is { } exp ? Math.Max(0, (exp - DateTimeOffset.UtcNow).TotalSeconds) : 0;

    public double LeaseFraction =>
        TtlSeconds > 0 ? Math.Clamp(LeaseSecondsRemaining / TtlSeconds, 0, 1) : 0;
}

/// <summary>
/// Shared state that only the true lock holder should modify. It accepts a write only when the
/// caller's fencing token is at least the highest token it has ever seen — so a stale holder
/// (one whose lock expired) is rejected. This demonstrates why fencing tokens matter.
/// </summary>
public class ProtectedResource
{
    private readonly object _sync = new();

    public string Name { get; }
    public long WriteCount { get; private set; }
    public long RejectedCount { get; private set; }
    public long HighestTokenSeen { get; private set; }
    public string? LastWriter { get; private set; }
    public long LastToken { get; private set; }
    public DateTimeOffset LastWriteUtc { get; private set; }

    public ProtectedResource(string name) => Name = name;

    public bool TryWrite(string writer, string color, long fencingToken)
    {
        lock (_sync)
        {
            if (fencingToken < HighestTokenSeen)
            {
                RejectedCount++;
                LastRejectedWriter = writer;
                LastRejectedColor = color;
                LastRejectedToken = fencingToken;
                return false;
            }

            HighestTokenSeen = fencingToken;
            WriteCount++;
            LastWriter = writer;
            LastWriterColor = color;
            LastToken = fencingToken;
            LastWriteUtc = DateTimeOffset.UtcNow;
            return true;
        }
    }

    public string? LastWriterColor { get; private set; }
    public string? LastRejectedWriter { get; private set; }
    public string? LastRejectedColor { get; private set; }
    public long LastRejectedToken { get; private set; }
}

public enum LogLevel { Info, Success, Warn, Error }

public record LogEntry(DateTimeOffset TimeUtc, string Worker, string Color, string Message, LogLevel Level);
