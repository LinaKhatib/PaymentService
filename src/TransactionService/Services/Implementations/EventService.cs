using TransactionService.Data.DTOs;
using TransactionService.Data.Interfaces;
using TransactionService.Exceptions;
using TransactionService.Models;

namespace TransactionService.Services;

public class EventService(IEventRepository eventRepository, ILogger<Program> logger) : IEventService
{
    public async Task<List<EventResponse>> GetEventsByOperationIdAsync(string operationId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Запрос истории событий для операции: {@OperationInfo}", 
            new{OperationId = operationId});

        var events = await eventRepository.GetByOperationIdAsync(operationId, cancellationToken);
        
        var eventsList = events?.ToList() ?? new List<Event>();  

        if (eventsList.Count == 0)
        {
            logger.LogWarning(
                "События операции {@OperationInfo} не найдены", 
                new{OperationId = operationId});
            
            throw new NotFoundException($"События операции {operationId} не найдены.");
        }
         
        var eventResponses = eventsList.Select(e => new EventResponse
        {
            EventId = e.EventId,
            Type = e.Type.ToString(),
            FromStatus = e.FromStatus?.ToString(),
            ToStatus = e.ToStatus.ToString(),
            Message = e.Message,
            OccurredAt = e.OccurredAt.ToString("O")
        }).ToList();
        logger.LogInformation(
            "Найдено {Count} событий для операции {@OperationInfo}", 
            eventResponses.Count, new{OperationId = operationId});
        
        return eventResponses;
    }
}
