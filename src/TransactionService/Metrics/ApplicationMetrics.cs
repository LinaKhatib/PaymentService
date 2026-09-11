using System.Diagnostics.Metrics;

namespace TransactionService.Metrics;

public class ApplicationMetrics
{
    private readonly Meter _meter;
    
    private readonly Counter<long> _retryCounter;

    private readonly UpDownCounter<long> _pendingOperationsCounter;

    public ApplicationMetrics()
    {
        _meter = new Meter("TransactionService", "1.0.0");
        
        _retryCounter = _meter.CreateCounter<long>(
            name: "provider_retry_total",
            unit: "retries",
            description: "Общее количество повторов к провайдеру");

        _pendingOperationsCounter = _meter.CreateUpDownCounter<long>(
            name: "operations_pending_count",
            unit: "operations",
            description: "Текущее количество операций в статусе PROCESSING");
    }

    public void RecordRetry()
    {
        _retryCounter.Add(1);
    }

    public void IncrementPending()
    {
        _pendingOperationsCounter.Add(1);
    }

    public void DecrementPending()
    {
        _pendingOperationsCounter.Add(-1);
    }
}