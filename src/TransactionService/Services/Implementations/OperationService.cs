using TransactionService.Data.DTOs;
using TransactionService.Data.Interfaces;
using TransactionService.Exceptions;
using TransactionService.Models;
using TransactionService.Models.Enums;

namespace TransactionService.Services;

public class OperationService(IOperationRepository operationRepository, IEventRepository eventRepository, ILogger<OperationService> logger, IEventFactory eventFactory) : IOperationService
{
    public async Task<OperationResponse> CreateOperationAsync(OperationRequest request, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Создание операции. {@OperationInfo}",
            new {
                request.OperationId,
                request.Amount,
                request.Currency
            });
        
        if (await operationRepository.ExistsOperationAsync(request.OperationId, cancellationToken))
        {
            logger.LogWarning(
                "Операция {@OperationInfo} уже существует",
                new {request.OperationId});
            
            throw new ConflictException($"Операция {request.OperationId} уже существует.");
        }

        var newOperation = new Operation
        {
            OperationId = request.OperationId,
            Amount = request.Amount,
            Currency = request.Currency,
            Description = request.Description,
            Status = OperationStatus.CREATED
        };
        await operationRepository.CreateOperationAsync(newOperation, cancellationToken);
        
        logger.LogInformation(
            "Операция создана и сохранена в БД. {@OperationInfo}", 
            new
            {
                newOperation.OperationId,
                newOperation.Status,
                newOperation.Amount,
                newOperation.Currency
            });
        
        var newEvent = eventFactory.CreateEventAsync(
            newOperation, 
            EventType.CREATED, 
            "Operation created", 
            null, 
            OperationStatus.CREATED);
        
        await eventRepository.AddEventAsync(newEvent, cancellationToken);
        
        logger.LogInformation(
            "Событие создано. {@EventInfo}", 
            new
            {
                newOperation.OperationId,
                newOperation.Status,
                
                newEvent.EventId,
                newEvent.Type,
                newEvent.OccurredAt
            });

        return MapToResponse(newOperation);
    }

    public async Task<(OperationResponse, bool StatusChanged)> SubmitOperationAsync(string operationId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Отправка провайдеру запроса на создание операции {@OperationInfo}", 
            new {operationId});
        
        var operation = await operationRepository.GetByOperationIdAsync(operationId, cancellationToken);

        if (operation == null)
        {
            logger.LogWarning(
                "Операция {@OperationInfo} не найдена.", 
                new {operationId});
            
            throw new NotFoundException($"Операция {operationId} не найдена.");
        }

        if (operation.Status != OperationStatus.CREATED)
        {
            logger.LogInformation(
                "Запрос провайдеру на создание операции {@OperationInfo} ранее уже был отправлен", 
                new {operationId});
            
            return (MapToResponse(operation), false);
        }
        
        operation.Status = OperationStatus.PROCESSING;
        await operationRepository.UpdateOperationAsync(operation, cancellationToken);
        
        var newEvent = eventFactory.CreateEventAsync(
            operation, 
            EventType.SUBMIT_ATTEMPT, 
            "Submit initiated, waiting for provider...", 
            OperationStatus.CREATED);
        
        await eventRepository.AddEventAsync(newEvent, cancellationToken);
        
        logger.LogInformation(
            "Событие создано. {@EventInfo}",
            new 
            {
                operation.OperationId,
                operation.Status,
                
                newEvent.EventId,
                newEvent.Type,
                newEvent.OccurredAt
            });
        
        return (MapToResponse(operation), true);
    }

    public async Task<OperationResponse> GetOperationAsync(string operationId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Запрос на получение стауса операции {@OperationInfo}", 
            new {operationId});
        
        var operation = await operationRepository.GetByOperationIdAsync(operationId, cancellationToken);

        if (operation == null)
        {
            logger.LogWarning(
                "Операция {@OperationInfo} не найдена", 
                new {operationId});
            
            throw new NotFoundException($"Операция {operationId} не найдена.");
        }
        logger.LogInformation(
            "Cтатус операции {@OperationInfo}", 
            new { operationId, operation.Status });
        
        return MapToResponse(operation);
    }
    
    public async Task HandleReceiptAsync(ReceiptRequest receipt, CancellationToken cancellationToken = default)
    {
        var operation = await operationRepository.GetByOperationIdAsync(receipt.OperationId, cancellationToken);
        
        // операции не существует
        if (operation == null)
        {
            logger.LogWarning(
                "Операция {@OperationInfo} не найдена для пришедшей квитанции",
                new {receipt.OperationId});
            
            throw new NotFoundException($"Операция {receipt.OperationId} не найдена");
        }

        // в операции уже есть ProviderPaymentId, а ProviderPaymentId из квитанции несоответстует
        if (operation.ProviderPaymentId != null && operation.ProviderPaymentId != receipt.ProviderPaymentId)
        {
            logger.LogWarning(
                "ProviderPaymentId {receipt.ProviderPaymentId} несоответствует операции. {@OperationInfo}",
                receipt.ProviderPaymentId, new {operation.OperationId, operation.ProviderPaymentId});
            
            throw new ConflictException(
                $"ProviderPaymentId несоответствует: {operation.ProviderPaymentId} vs {receipt.ProviderPaymentId}"
            );
        }

        // у операции не было ProviderPaymentId. ProviderPaymentId из квитанции сохраняется
        if (operation.ProviderPaymentId == null)
        {
            operation.ProviderPaymentId = receipt.ProviderPaymentId;
            await operationRepository.UpdateOperationAsync(operation, cancellationToken);
            
            logger.LogInformation(
                "Сохранение ProviderPaymentId {ProviderPaymentId} из квитанции в операцию {@OperationInfo}", 
                receipt.ProviderPaymentId, new {operation.OperationId, operation.ProviderPaymentId});
        }

        if (operation.Status == OperationStatus.COMPLETED || operation.Status == OperationStatus.REJECTED)
        {
            await eventRepository.AddEventAsync(eventFactory.CreateEventAsync(
                operation, 
                EventType.IGNORED, 
                $"Ignored {receipt.Result} callback, already {operation.Status}", 
                operation.Status,
                operation.Status), cancellationToken);
            
            logger.LogWarning(
                "Квитанция игнорируется, так как операция уже в финальном статусе {@OperationInfo}", 
                new {operation.OperationId, operation.Status});
            return;
        }
        
        if (!Enum.TryParse<OperationStatus>(receipt.Result.ToUpper(), ignoreCase: true, out var newStatus) ||
            (newStatus != OperationStatus.COMPLETED && newStatus != OperationStatus.REJECTED))
        {
            throw new BadRequestException($"Недопустимый результат: {receipt.Result}. Ожидалось COMPLETED или REJECTED.");
        }

        var eventType = newStatus == OperationStatus.COMPLETED ? EventType.COMPLETED : EventType.REJECTED; 
        
        operation.Status = newStatus; 
        await operationRepository.UpdateOperationAsync(operation, cancellationToken);

        await eventRepository.AddEventAsync(eventFactory.CreateEventAsync(
            operation, 
            eventType, 
            receipt.Message, 
            toStatus: operation.Status), cancellationToken);
        
        logger.LogInformation(
            "Получен финальный статус для операции. {@OperationInfo}", 
            new {operation.OperationId, operation.Status});
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