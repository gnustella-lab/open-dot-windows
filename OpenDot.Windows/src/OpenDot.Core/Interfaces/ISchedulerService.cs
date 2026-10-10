namespace OpenDot.Core.Interfaces;

public interface ISchedulerService
{
    void Start();
    void Stop();
    void Schedule(string routineId);
    void Unschedule(string routineId);
    bool IsValidCron(string expression);
    DateTimeOffset? GetNextOccurrence(string expression);
}
