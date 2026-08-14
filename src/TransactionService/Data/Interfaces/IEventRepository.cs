using TransactionService.Models;
using TransactionService.Models.Enums;

namespace TransactionService.Data.Interfaces;

public interface IEventRepository
{
    Task<Event> AddEventAsync(Event newEvent);
    Task<IEnumerable<Event>> GetByOperationIdAsync(string operationId);
    Task<bool> HasEventAsync(string operationId, EventType type);
}