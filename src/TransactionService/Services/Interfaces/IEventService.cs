using TransactionService.Data.DTOs;

namespace TransactionService.Services;

public interface IEventService
{
    Task<List<EventResponse>> GetEventsByOperationIdAsync(string operationId, CancellationToken cancellationToken = default);
}