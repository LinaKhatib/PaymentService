using TransactionService.Data.DTOs;
using TransactionService.Data.Interfaces;
using TransactionService.Models;
using TransactionService.Models.Enums;

namespace TransactionService.Services;

public class OperationService(IOperationRepository operationRepository, IEventRepository eventRepository, IProviderService providerService, ILogger<OperationService> logger) : IOperationService
{
    public async Task<OperationResponse> CreateOperationAsync(OperationRequest request)
    {
        logger.LogInformation("--- Создание операции: {OperationId}", request.OperationId);
        
        if (await operationRepository.ExistsOperationAsync(request.OperationId))
        {
            logger.LogWarning("--- Операция уже существует: {OperationId}", request.OperationId);
            throw new InvalidOperationException($"Операция {request.OperationId} уже существует.");
        }

        var newOperation = new Operation
        {
            OperationId = request.OperationId,
            Amount = request.Amount,
            Currency = request.Currency,
            Description = request.Description,
            Status = OperationStatus.CREATED
        };
        await operationRepository.CreateOperationAsync(newOperation);
        logger.LogInformation("--- Операция сохранена в БД: {OperationId}", newOperation.OperationId);
        
        var newEvent = new Event
        {
            OperationId = newOperation.OperationId,
            Type = EventType.CREATED,
            ToStatus = newOperation.Status,
            Message = "Operation created",
            OccurredAt =  DateTime.UtcNow,
            Operation = newOperation
        };
        await eventRepository.AddEventAsync(newEvent);
        logger.LogInformation("--- Событие создано для операции {OperationId}: {EventType}", newOperation.OperationId, newEvent.Type);

        return MapToResponse(newOperation);
    }

    public async Task<(OperationResponse, bool StatusChanged)> SubmitOperationAsync(string operationId)
    {
        logger.LogInformation("--- Отправка провайдеру запроса на создание операции: {OperationId}", operationId);
        var operation = await operationRepository.GetByOperationIdAsync(operationId);

        if (operation == null)
        {
            logger.LogWarning("--- Операция не найдена: {OperationId}", operationId);
            throw new KeyNotFoundException($"Операция {operationId} не найдена.");
        }

        if (operation.Status != OperationStatus.CREATED)
        {
            logger.LogInformation("--- Запрос на создание операции провайдеру ранее уже был создан: {OperationId}", operationId);
            return (MapToResponse(operation), false);
        }
        
        operation.Status = OperationStatus.PROCESSING;
        await operationRepository.UpdateOperationAsync(operation);
        
        var newEvent = new Event
        {
            OperationId = operation.OperationId,
            Type = EventType.SUBMIT_ATTEMPT,
            FromStatus = OperationStatus.CREATED,
            ToStatus = OperationStatus.PROCESSING,
            Message = "Submit initiated, waiting for provider...",
            OccurredAt =  DateTime.UtcNow,
            Operation = operation
        };
        await eventRepository.AddEventAsync(newEvent);
        logger.LogInformation("--- Событие создано для операции {OperationId}: {EventType}. А операция переведена в статус {OperationStatus}", operation.OperationId, newEvent.Type, operation.Status);

        try
        {
            var providerResponse = await providerService.SendPaymentAsync(operation.OperationId, operation.Amount, operation.Currency);
            logger.LogInformation("--- Запрос провайдеру на создание операции {OperationId} создан и получен ответ ProviderPaymentId: {ProviderPaymentId}, Status: {Status}", operation.OperationId, providerResponse.ProviderPaymentId, providerResponse.Status);
            
            operation.ProviderPaymentId = providerResponse.ProviderPaymentId;
            await operationRepository.UpdateOperationAsync(operation);
            logger.LogInformation("--- Id операции {OperationId} у провайдера сохранён: {ProviderPaymentId}", operationId, operation.ProviderPaymentId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при вызове провайдера для {OperationId}. Операция остается в состоянии PROCESSING", operationId);
            throw;
        }
        
        return (MapToResponse(operation), true);
    }

    public async Task<OperationResponse> GetOperationAsync(string operationId)
    {
        logger.LogInformation("--- Запрос на получение стауса операции: {OperationId}", operationId);
        var operation = await operationRepository.GetByOperationIdAsync(operationId);

        if (operation == null)
        {
            logger.LogWarning("--- Операции не найдена: {OperationId}", operationId);
            throw new KeyNotFoundException($"Операция {operationId} не найдена.");
        }
        logger.LogInformation("--- Cтатус операции {OperationId}: {Status}", operationId, operation.Status);
        
        return MapToResponse(operation);
    }

    private OperationResponse MapToResponse(Operation operation)
    {
        return new OperationResponse
        {
            OperationId = operation.OperationId,
            Amount = operation.Amount,
            Currency = operation.Currency,
            Description = operation.Description,
            Status = operation.Status.ToString(),
            ProviderPaymentId = operation.ProviderPaymentId
        };
    }
}