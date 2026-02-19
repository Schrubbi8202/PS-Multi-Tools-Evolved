using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using PSMultiTools.Core;

namespace PSMultiTools.Infrastructure;

public sealed class JobService : IJobService
{
    private sealed class JobRecord
    {
        public required JobStatusDto Status { get; set; }
        public Channel<JobProgressEventDto> Events { get; } = Channel.CreateUnbounded<JobProgressEventDto>();
    }

    private readonly ConcurrentDictionary<Guid, JobRecord> _jobs = new();

    public Guid Create(string name)
    {
        var jobId = Guid.NewGuid();
        var initial = new JobStatusDto(
            JobId: jobId,
            Name: name,
            State: JobState.Queued,
            ProgressPercent: 0,
            Message: "Queued",
            CreatedAt: DateTimeOffset.UtcNow,
            FinishedAt: null
        );
        _jobs[jobId] = new JobRecord { Status = initial };
        Publish(initial, "Queued");
        return jobId;
    }

    public JobStatusDto Get(Guid jobId)
    {
        if (_jobs.TryGetValue(jobId, out var record))
        {
            return record.Status;
        }

        throw new KeyNotFoundException($"Job '{jobId}' was not found.");
    }

    public IReadOnlyCollection<JobStatusDto> GetAll()
    {
        return _jobs.Values.Select(v => v.Status).ToArray();
    }

    public async IAsyncEnumerable<JobProgressEventDto> Stream(Guid jobId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!_jobs.TryGetValue(jobId, out var record))
        {
            yield break;
        }

        await foreach (var evt in record.Events.Reader.ReadAllAsync(cancellationToken))
        {
            yield return evt;
        }
    }

    public void Update(Guid jobId, JobState state, int progressPercent, string message)
    {
        if (!_jobs.TryGetValue(jobId, out var record))
        {
            return;
        }

        DateTimeOffset? finished = state is JobState.Completed or JobState.Failed ? DateTimeOffset.UtcNow : null;
        var updated = record.Status with
        {
            State = state,
            ProgressPercent = Math.Clamp(progressPercent, 0, 100),
            Message = message,
            FinishedAt = finished ?? record.Status.FinishedAt
        };

        record.Status = updated;
        Publish(updated, message);

        if (state is JobState.Completed or JobState.Failed)
        {
            record.Events.Writer.TryComplete();
        }
    }

    private void Publish(JobStatusDto status, string message)
    {
        if (_jobs.TryGetValue(status.JobId, out var record))
        {
            record.Events.Writer.TryWrite(new JobProgressEventDto(
                status.JobId,
                status.State,
                status.ProgressPercent,
                message,
                DateTimeOffset.UtcNow
            ));
        }
    }
}
