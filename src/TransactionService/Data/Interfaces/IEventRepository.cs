using TransactionService.Models;
using TransactionService.Models.Enums;

namespace TransactionService.Data.Interfaces;

public interface IEventRepository
{
    Task<Event> AddEventAsync(Event newEvent, CancellationToken cancellationToken = default);
    Task<IEnumerable<Event>> GetByOperationIdAsync(string operationId, CancellationToken cancellationToken = default);
    Task<bool> HasEventAsync(string operationId, EventType type, CancellationToken cancellationToken = default);
}