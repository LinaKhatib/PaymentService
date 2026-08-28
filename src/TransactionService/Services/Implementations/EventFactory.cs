using TransactionService.Models;
using TransactionService.Models.Enums;

namespace TransactionService.Services;

public class EventFactory : IEventFactory
{
    public Event CreateEventAsync(Operation operation,
        EventType type,
        string message,
        OperationStatus? fromStatus = OperationStatus.PROCESSING,
        OperationStatus toStatus = OperationStatus.PROCESSING)
    {
        return new Event
        {
            OperationId = operation.OperationId,
            Type = type,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            Message = message,
            OccurredAt = DateTime.UtcNow,
            Operation = operation
        };
    }
    
}