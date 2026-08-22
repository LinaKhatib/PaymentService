using TransactionService.Data.DTOs;

namespace TransactionService.Services;

public interface IEventService
{
    Task<IEnumerable<EventResponse>> GetEventsByOperationIdAsync(string operationId);
}