using System.Collections.Concurrent;
using Cosmos.DistributedLock;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;

namespace DistributedLockWeb.Services;

/// <summary>
/// Drives the on-screen "workers" that compete for real Azure Cosmos DB distributed locks and
/// exposes their live state to the Blazor UI. Every acquire / renew / release is a real
/// operation against Cosmos via the <c>CosmosDistributedLock</c> library.
/// </summary>
public class SimulationService
{
    private static readonly string[] Palette =
        { "#38bdf8", "#f472b6", "#f59e0b", "#a78bfa", "#34d399", "#fb7185", "#60a5fa", "#facc15" };

    private readonly ICosmosDistributedLockProviderFactory _factory;
    private readonly string _endpoint;
    private readonly string? _key;
    private readonly string _databaseName;
    private readonly string _containerName;

    private readonly List<Worker> _workers = new();
    private readonly ConcurrentDictionary<string, ProtectedResource> _resources = new();
    private readonly LinkedList<LogEntry> _log = new();
    private readonly object _sync = new();

    private int _ttlSeconds = 8;
    private double _pace = 1.0; // >1 = idle workers retry faster
    private int _initialized;
    private int _nameCounter;

    public IReadOnlyList<string> LockNames { get; } = new[] { "resource-A", "resource-B" };

    public event Action? Changed;

    public SimulationService(ICosmosDistributedLockProviderFactory factory, string endpoint, string? key, string databaseName, string containerName)
    {
        _factory = factory;
        _endpoint = endpoint;
        _key = key;
        _databaseName = databaseName;
        _containerName = containerName;

        foreach (var n in new[] { "Worker-A", "Worker-B", "Worker-C" })
        {
            AddWorkerInternal(n);
        }
    }

    public int TtlSeconds => _ttlSeconds;
    public double Pace => _pace;

    public IReadOnlyList<Worker> Workers
    {
        get { lock (_sync) { return _workers.ToList(); } }
    }

    public IReadOnlyList<ProtectedResource> ActiveResources
    {
        get
        {
            var names = Workers.Select(w => w.LockName).Distinct();
            return names.Select(GetResource).ToList();
        }
    }

    public IReadOnlyList<LogEntry> RecentLog
    {
        get { lock (_sync) { return _log.ToList(); } }
    }

    public ProtectedResource GetResource(string lockName) =>
        _resources.GetOrAdd(lockName, n => new ProtectedResource(n));

    /// <summary>Synthesizes the Cosmos lock document JSON for a lock name from its current holder.</summary>
    public string CurrentLockDocumentJson(string lockName)
    {
        var holder = Workers.FirstOrDefault(w => w.LockName == lockName && w.State == WorkerState.Holding && !w.LockExpiredWhileWorking);
        if (holder is null)
        {
            return $"// no active lock document for \"{lockName}\"\n// (the id would be inserted on acquire)";
        }

        var doc = new
        {
            id = lockName,
            name = lockName,
            owner = holder.Name,
            fencingToken = holder.FencingToken,
            lockObtainedAt = holder.HoldStartedUtc,
            lockLastRenewedAt = holder.LeaseExpiresUtc?.AddSeconds(-holder.TtlSeconds),
            ttl = holder.TtlSeconds
        };
        return JsonConvert.SerializeObject(doc, Formatting.Indented);
    }

    public async Task EnsureInitializedAsync()
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1) return;

        try
        {
            using CosmosClient bootstrap = CosmosClientFactory.Create(_endpoint, _key);
            Database db = await bootstrap.CreateDatabaseIfNotExistsAsync(_databaseName);
            await db.CreateContainerIfNotExistsAsync(new ContainerProperties
            {
                Id = _containerName,
                PartitionKeyPath = "/id",
                DefaultTimeToLive = -1
            });
        }
        catch
        {
            // Allow retry on a later call (e.g., transient emulator warmup).
            Interlocked.Exchange(ref _initialized, 0);
            throw;
        }
    }

    // ---- Worker management ----------------------------------------------------------------

    private Worker AddWorkerInternal(string? name = null)
    {
        var w = new Worker
        {
            Name = name ?? $"Worker-{(char)('A' + _nameCounter)}",
            Color = Palette[_nameCounter % Palette.Length],
            LockName = "resource-A",
            WorkDurationSeconds = 6,
            RenewalEnabled = true
        };
        _nameCounter++;
        lock (_sync) { _workers.Add(w); }
        return w;
    }

    public void AddWorker()
    {
        AddWorkerInternal();
        Notify();
    }

    public void RemoveWorker(Guid id)
    {
        Worker? w;
        lock (_sync) { w = _workers.FirstOrDefault(x => x.Id == id); }
        if (w is null) return;
        StopWorker(id);
        lock (_sync) { _workers.Remove(w); }
        Notify();
    }

    public async Task StartAllAsync()
    {
        await EnsureInitializedAsync();
        foreach (var w in Workers) StartWorker(w.Id);
    }

    public void StartWorker(Guid id)
    {
        var w = Find(id);
        if (w is null || w.Running) return;

        w.Running = true;
        w.State = WorkerState.Idle;
        w.Status = null;
        w.Cts = new CancellationTokenSource();
        w.LoopTask = Task.Run(() => RunWorkerAsync(w, w.Cts.Token));
        Notify();
    }

    public void StopWorker(Guid id)
    {
        var w = Find(id);
        if (w is null) return;
        w.Cts?.Cancel();
        w.Running = false;
        w.State = WorkerState.Idle;
        w.Status = "stopped";
        w.HoldStartedUtc = null;
        w.WorkEndsUtc = null;
        w.LeaseExpiresUtc = null;
        Notify();
    }

    public void StopAll()
    {
        foreach (var w in Workers) StopWorker(w.Id);
    }

    /// <summary>Simulate a crash: the worker stops WITHOUT releasing its lock, which then expires via TTL.</summary>
    public void CrashWorker(Guid id)
    {
        var w = Find(id);
        if (w is null) return;

        if (w.State == WorkerState.Holding)
        {
            w.State = WorkerState.Crashed; // the loop abandons the lock and exits
        }
        else
        {
            StopWorker(id);
        }
        Notify();
    }

    public void SetWorkDuration(Guid id, int seconds) { var w = Find(id); if (w != null) { w.WorkDurationSeconds = Math.Clamp(seconds, 1, 30); Notify(); } }
    public void SetRenewal(Guid id, bool enabled) { var w = Find(id); if (w != null) { w.RenewalEnabled = enabled; Notify(); } }
    public void SetWaitMode(Guid id, bool wait) { var w = Find(id); if (w != null) { w.WaitMode = wait; Notify(); } }
    public void SetWaitTimeout(Guid id, int seconds) { var w = Find(id); if (w != null) { w.WaitTimeoutSeconds = Math.Clamp(seconds, 1, 30); Notify(); } }
    public void SetLockName(Guid id, string name) { var w = Find(id); if (w != null) { w.LockName = name; Notify(); } }
    public void SetTtl(int seconds) { _ttlSeconds = Math.Clamp(seconds, 2, 30); Notify(); }
    public void SetPace(double pace) { _pace = Math.Clamp(pace, 0.25, 3.0); Notify(); }

    public async Task ResetAsync()
    {
        StopAll();
        await Task.Delay(200);
        lock (_sync)
        {
            _log.Clear();
        }
        _resources.Clear();
        Notify();
    }

    // ---- Worker loop ----------------------------------------------------------------------

    private async Task RunWorkerAsync(Worker w, CancellationToken ct)
    {
        var provider = _factory.GetLockProvider();

        try
        {
            while (!ct.IsCancellationRequested)
            {
                w.State = WorkerState.Trying;
                w.Status = "acquiring…";
                w.LockExpiredWhileWorking = false;
                Notify();
                Log(w, $"attempting to acquire [{w.LockName}]…", LogLevel.Info);

                CosmosDistributedLock @lock;
                if (w.WaitMode)
                {
                    w.State = WorkerState.Waiting;
                    Notify();
                    @lock = await provider.AcquireLockAsync(w.LockName, TimeSpan.FromSeconds(w.WaitTimeoutSeconds), autoRenew: false, ttlSeconds: _ttlSeconds);
                }
                else
                {
                    @lock = await provider.TryAcquireLockAsync(w.LockName, autoRenew: false, ttlSeconds: _ttlSeconds);
                }

                if (!@lock.IsAcquired)
                {
                    @lock.Dispose();
                    w.State = WorkerState.Idle;
                    w.Status = w.WaitMode ? "gave up waiting" : "held by another";
                    Log(w, w.WaitMode
                        ? $"gave up after {w.WaitTimeoutSeconds}s — [{w.LockName}] still held"
                        : $"could not acquire [{w.LockName}] — held by another", LogLevel.Warn);
                    Notify();
                    await DelayScaled(900, ct);
                    continue;
                }

                // --- Acquired the lock ---
                w.CurrentLock = @lock;
                w.State = WorkerState.Holding;
                w.FencingToken = @lock.FencingToken;
                w.TtlSeconds = @lock.Ttl > 0 ? @lock.Ttl : _ttlSeconds;
                w.HoldStartedUtc = DateTimeOffset.UtcNow;
                w.WorkEndsUtc = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(w.WorkDurationSeconds);
                w.LeaseExpiresUtc = (@lock.LockLastRenewedAt ?? DateTimeOffset.UtcNow) + TimeSpan.FromSeconds(w.TtlSeconds);
                w.RenewCount = 0;
                Log(w, $"ACQUIRED [{w.LockName}] · fencing token {w.FencingToken}", LogLevel.Success);
                Notify();

                var resource = GetResource(w.LockName);
                bool renewFailed = false;

                while (DateTimeOffset.UtcNow < w.WorkEndsUtc && w.State == WorkerState.Holding && !ct.IsCancellationRequested)
                {
                    // The holder does work by writing to a shared resource, stamped with its
                    // fencing token. A stale holder's writes are rejected (see ProtectedResource).
                    bool accepted = resource.TryWrite(w.Name, w.Color, w.FencingToken);
                    if (!accepted && !w.LockExpiredWhileWorking)
                    {
                        Log(w, $"write to [{w.LockName}] REJECTED — fencing token {w.FencingToken} is stale", LogLevel.Error);
                    }

                    var expiresAt = w.LeaseExpiresUtc!.Value;
                    if (w.RenewalEnabled)
                    {
                        if (DateTimeOffset.UtcNow >= expiresAt - TimeSpan.FromSeconds(1.5))
                        {
                            bool renewed = await @lock.TryRenewAsync();
                            if (renewed)
                            {
                                w.RenewCount++;
                                w.LeaseExpiresUtc = (@lock.LockLastRenewedAt ?? DateTimeOffset.UtcNow) + TimeSpan.FromSeconds(w.TtlSeconds);
                                Log(w, $"renewed lease on [{w.LockName}] — still working", LogLevel.Info);
                                Notify();
                            }
                            else
                            {
                                renewFailed = true;
                                Log(w, $"lost [{w.LockName}] — renewal failed", LogLevel.Error);
                                break;
                            }
                        }
                    }
                    else if (!w.LockExpiredWhileWorking && DateTimeOffset.UtcNow >= expiresAt)
                    {
                        w.LockExpiredWhileWorking = true;
                        w.Status = "lease EXPIRED — still working!";
                        Log(w, $"lease on [{w.LockName}] EXPIRED but work continues (renewal OFF) — danger!", LogLevel.Error);
                        Notify();
                    }

                    await DelayScaled(600, ct);
                }

                if (w.State == WorkerState.Crashed)
                {
                    // Abandon the lock (no release) — Cosmos TTL will delete it.
                    Log(w, $"CRASHED holding [{w.LockName}] — lock left to expire via TTL (no deadlock)", LogLevel.Error);
                    w.CurrentLock = null;
                    w.HoldStartedUtc = null; w.WorkEndsUtc = null; w.LeaseExpiresUtc = null;
                    w.Running = false;
                    Notify();
                    return;
                }

                if (renewFailed)
                {
                    @lock.Dispose();
                    w.CurrentLock = null;
                    w.State = WorkerState.Idle;
                    w.Status = "lost the lock";
                    w.HoldStartedUtc = null; w.WorkEndsUtc = null; w.LeaseExpiresUtc = null;
                    Notify();
                    await DelayScaled(900, ct);
                    continue;
                }

                // Work finished — release the lock (delete). If it already expired, this is a no-op.
                @lock.Dispose();
                w.CurrentLock = null;
                w.State = WorkerState.Idle;
                w.Status = w.LockExpiredWhileWorking ? "finished (but had lost the lock!)" : "released";
                w.HoldStartedUtc = null; w.WorkEndsUtc = null; w.LeaseExpiresUtc = null;
                Log(w, $"finished work — released [{w.LockName}]", LogLevel.Info);
                Notify();
                await DelayScaled(900, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // stopping
        }
        catch (Exception ex)
        {
            Log(w, $"error: {ex.Message}", LogLevel.Error);
        }
        finally
        {
            try { w.CurrentLock?.Dispose(); } catch { }
            w.CurrentLock = null;
            if (w.State != WorkerState.Crashed) { w.State = WorkerState.Idle; }
            w.Running = false;
            Notify();
        }
    }

    private async Task DelayScaled(int ms, CancellationToken ct)
    {
        int scaled = Math.Max(50, (int)(ms / _pace));
        await Task.Delay(scaled, ct);
    }

    private Worker? Find(Guid id)
    {
        lock (_sync) { return _workers.FirstOrDefault(x => x.Id == id); }
    }

    private void Log(Worker w, string message, LogLevel level)
    {
        lock (_sync)
        {
            _log.AddFirst(new LogEntry(DateTimeOffset.UtcNow, w.Name, w.Color, message, level));
            while (_log.Count > 120) _log.RemoveLast();
        }
    }

    private void Notify() => Changed?.Invoke();
}
