using TransactionService.Models;
using TransactionService.Models.Enums;

namespace TransactionService.Services;

public interface IEventFactory
{
    Event CreateEventAsync(Operation operation,
        EventType type,
        string message,
        OperationStatus? fromStatus = OperationStatus.PROCESSING,
        OperationStatus toStatus = OperationStatus.PROCESSING);
}