using Microsoft.EntityFrameworkCore;
using TransactionService.Data.Interfaces;
using TransactionService.Models;
using TransactionService.Models.Enums;

namespace TransactionService.Data.Repositories;

public class EventRepository(PaymentDbContext context) : IEventRepository
{
    public async Task<Event> AddEventAsync(Event newEvent)
    {
        await context.Events.AddAsync(newEvent);
        await context.SaveChangesAsync();
        
        return newEvent;
    }

    public async Task<IEnumerable<Event>> GetByOperationIdAsync(string operationId)
    {
        return await context.Events
            .Where(e => e.OperationId == operationId)
            .OrderBy(e => e.OccurredAt)
            .ToListAsync();
    }

    public async Task<bool> HasEventAsync(string operationId, EventType type)
    {
        return await context.Events
            .AnyAsync(e => e.OperationId == operationId && e.Type == type);
    }
}