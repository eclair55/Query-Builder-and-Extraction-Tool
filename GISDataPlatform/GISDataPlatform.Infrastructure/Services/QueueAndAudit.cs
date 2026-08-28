using System.Collections.Concurrent;
using GISDataPlatform.Domain.Models;

namespace GISDataPlatform.Infrastructure.Services;

public class ExtractionJobQueue
{
    private readonly ConcurrentQueue<ExtractionJob> _jobs = new();
    private readonly ConcurrentDictionary<Guid, ExtractionJob> _jobStore = new();

    public void Enqueue(ExtractionJob job)
    {
        _jobStore[job.Id] = job;
        _jobs.Enqueue(job);
    }

    public bool TryDequeue(out ExtractionJob? job)
    {
        return _jobs.TryDequeue(out job);
    }

    public ExtractionJob? GetJob(Guid id)
    {
        _jobStore.TryGetValue(id, out var job);
        return job;
    }

    public IEnumerable<ExtractionJob> GetAllJobs()
    {
        return _jobStore.Values;
    }
}

public class AuditLogger
{
    private readonly ConcurrentBag<AuditLog> _logs = new();

    public void Log(AuditLog log)
    {
        _logs.Add(log);
    }

    public IEnumerable<AuditLog> GetLogs()
    {
        return _logs.OrderByDescending(l => l.Timestamp);
    }
}
