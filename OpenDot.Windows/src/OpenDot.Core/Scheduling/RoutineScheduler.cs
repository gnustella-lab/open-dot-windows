using System.Collections.Concurrent;
using Cronos;
using OpenDot.Core.Interfaces;
using OpenDot.Core.Models;

namespace OpenDot.Core.Scheduling;

public class RoutineScheduler : ISchedulerService, IDisposable
{
    private readonly IDotRepository _repository;
    private readonly Func<IAgentRuntime> _runtimeProvider;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _scheduledJobs = new();
    private bool _started;

    public RoutineScheduler(IDotRepository repository, Func<IAgentRuntime> runtimeProvider)
    {
        _repository = repository;
        _runtimeProvider = runtimeProvider;
    }

    public void Start()
    {
        if (_started) return;
        _started = true;

        var routines = _repository.ListRoutines();
        foreach (var r in routines)
        {
            if (r.Enabled)
            {
                Schedule(r.Id);
            }
        }
    }

    public void Stop()
    {
        _started = false;
        foreach (var key in _scheduledJobs.Keys)
        {
            Unschedule(key);
        }
    }

    public void Schedule(string routineId)
    {
        Unschedule(routineId);
        var routine = _repository.GetRoutine(routineId);
        if (routine == null || !routine.Enabled) return;

        var cts = new CancellationTokenSource();
        _scheduledJobs[routineId] = cts;

        _ = Task.Run(() => RunJobLoopAsync(routineId, routine.Schedule, cts.Token), cts.Token);
    }

    public void Unschedule(string routineId)
    {
        if (_scheduledJobs.TryRemove(routineId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    public bool IsValidCron(string expression)
    {
        try
        {
            CronExpression.Parse(expression, CronFormat.Standard);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public DateTimeOffset? GetNextOccurrence(string expression)
    {
        try
        {
            var cron = CronExpression.Parse(expression, CronFormat.Standard);
            return cron.GetNextOccurrence(DateTimeOffset.UtcNow, TimeZoneInfo.Local);
        }
        catch
        {
            return null;
        }
    }

    private async Task RunJobLoopAsync(string routineId, string cronExpression, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var cron = CronExpression.Parse(cronExpression, CronFormat.Standard);
                var nextUtc = cron.GetNextOccurrence(DateTimeOffset.UtcNow, TimeZoneInfo.Local);
                if (nextUtc == null) break;

                var delay = nextUtc.Value - DateTimeOffset.UtcNow;
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, ct);
                }

                if (ct.IsCancellationRequested) break;

                var routine = _repository.GetRoutine(routineId);
                if (routine != null && routine.Enabled)
                {
                    routine.LastRunAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    routine.NextRunAt = cron.GetNextOccurrence(DateTimeOffset.UtcNow.AddSeconds(1), TimeZoneInfo.Local)?.ToUnixTimeMilliseconds();
                    _repository.UpdateRoutine(routine);

                    var runtime = _runtimeProvider();
                    await runtime.RunRoutineAsync(routine, ct);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Scheduler] Error in routine {routineId}: {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
            }
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
