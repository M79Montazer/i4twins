using I4Twins.Application.Persistence;
using I4Twins.Domain.Rules;

namespace I4Twins.IntegrationTests;

public sealed class NoopStore : IProcessingStore
{
    public ProcessingBatch? LastBatch { get; private set; }

    public PersistenceResult Persist(ProcessingBatch batch)
    {
        LastBatch = batch;
        return new PersistenceResult(batch.Evaluations.Count, 0, 0, batch.RaisedAlerts.Count);
    }
}
