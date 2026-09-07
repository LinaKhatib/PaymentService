using Microsoft.EntityFrameworkCore;
using TransactionService.Data.Interfaces;
using TransactionService.Models;
using TransactionService.Models.Enums;

namespace TransactionService.Data.Repositories;

public class EventRepository(PaymentDbContext context, ILogger<EventRepository> logger) : IEventRepository
{
    public async Task<Event> AddEventAsync(Event newEvent, CancellationToken cancellationToken = default)
    {
        logger.LogDebug(
            "Сохранение события для операции. {@EventInfo}",
            new { newEvent.OperationId });
        
        var maxEventId = await context.Events
            .Where(e => e.OperationId == newEvent.OperationId)
            .MaxAsync(e => (int?)e.EventId, cancellationToken) ?? 0;
        
        newEvent.EventId = maxEventId + 1;
        
        await context.Events.AddAsync(newEvent, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        
        logger.LogDebug(
            "Событие сохранено. {@EventInfo}",
            new { newEvent.Id, newEvent.EventId});
        
        return newEvent;
        
    }

    public async Task<IEnumerable<Event>> GetByOperationIdAsync(string operationId, CancellationToken cancellationToken = default)
    {
        logger.LogDebug(
            "Запрос событий операции. {@OperationInfo}",
            new { OperationId = operationId });
        
        return await context.Events
            .Where(e => e.OperationId == operationId)
            .OrderBy(e => e.OccurredAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasEventAsync(string operationId, EventType type, CancellationToken cancellationToken = default)
    {
        logger.LogDebug(
            "Запрос существования события типа {EventType} операции. {@OperationInfo}",
            type, new { OperationId = operationId });
        
        return await context.Events
            .AnyAsync(e => e.OperationId == operationId && e.Type == type, cancellationToken);
    }
}