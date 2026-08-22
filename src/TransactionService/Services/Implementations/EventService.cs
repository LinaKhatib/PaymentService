using TransactionService.Data.DTOs;
using TransactionService.Data.Interfaces;

namespace TransactionService.Services;

public class EventService(IEventRepository eventRepository, ILogger<Program> logger) : IEventService
{
    public async Task<IEnumerable<EventResponse>> GetEventsByOperationIdAsync(string operationId)
    {
        logger.LogInformation("Запрос истории событий для операции: {OperationId}", operationId);

        var events = await eventRepository.GetByOperationIdAsync(operationId);

        if (events == null || !events.Any())
        {
            logger.LogWarning("--- События операции {OperationId} не найдены", operationId);
            throw new KeyNotFoundException($"События операции {operationId} не найдены.");
        }
         
        var eventResponses = events.Select(e => new EventResponse
        {
            EventId = e.EventId,
            Type = e.Type.ToString(),
            FromStatus = e.FromStatus?.ToString(),
            ToStatus = e.ToStatus.ToString(),
            Message = e.Message,
            OccurredAt = e.OccurredAt.ToString()
        });
        logger.LogInformation("Найдено {Count} событий для операции {OperationId}", eventResponses.Count(), operationId);
        
        return eventResponses;
    }
}
