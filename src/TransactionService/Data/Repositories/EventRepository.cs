using Microsoft.EntityFrameworkCore;
using TransactionService.Data.Interfaces;
using TransactionService.Models;
using TransactionService.Models.Enums;

namespace TransactionService.Data.Repositories;

public class EventRepository(PaymentDbContext context, ILogger<EventRepository> logger) : IEventRepository
{
    public async Task<Event> AddEventAsync(Event newEvent)
    {
        logger.LogDebug("--- Сохранение события для операции: {OperationId}", newEvent.OperationId);
        var maxEventId = await context.Events
            .Where(e => e.OperationId == newEvent.OperationId)
            .MaxAsync(e => (int?)e.EventId) ?? 0;
        
        newEvent.EventId = maxEventId + 1; // ← Присваиваем логический EventId
        
        // Id (первичный ключ) сгенерируется БД автоматически!
        await context.Events.AddAsync(newEvent);
        await context.SaveChangesAsync();
        
        logger.LogDebug("Событие сохранено: Id={Id}, EventId={EventId}", newEvent.Id, newEvent.EventId);
        return newEvent;
        
    }

    public async Task<IEnumerable<Event>> GetByOperationIdAsync(string operationId)
    {
        logger.LogDebug("--- Запрос событий операции {OperationId}", operationId);
        
        return await context.Events
            .Where(e => e.OperationId == operationId)
            .OrderBy(e => e.OccurredAt)
            .ToListAsync();
    }

    public async Task<bool> HasEventAsync(string operationId, EventType type)
    {
        logger.LogDebug("--- Запрос существования события типа {EventType} операции {OperationId}", type, operationId);
        
        return await context.Events
            .AnyAsync(e => e.OperationId == operationId && e.Type == type);
    }
}