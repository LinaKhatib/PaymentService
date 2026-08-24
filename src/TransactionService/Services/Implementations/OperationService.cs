using TransactionService.Data.DTOs;
using TransactionService.Data.Interfaces;
using TransactionService.Exceptions;
using TransactionService.Models;
using TransactionService.Models.Enums;

namespace TransactionService.Services;

public class OperationService(IOperationRepository operationRepository, IEventRepository eventRepository, /*IProviderService providerService,*/ ILogger<OperationService> logger) : IOperationService
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
    
    public async Task HandleReceiptAsync(ReceiptRequest receipt)
    {
        var operation = await operationRepository.GetByOperationIdAsync(receipt.OperationId);
        
        // операции не существует
        if (operation == null)
        {
            logger.LogWarning("--- Операция {OperationId} не найдена для пришедшей квитанции", receipt.OperationId);
            throw new NotFoundException($"Операция {receipt.OperationId} не найдена");
        }

        // в операции уже есть ProviderPaymentId, а ProviderPaymentId из квитанции несоответстует
        if (operation.ProviderPaymentId != null && operation.ProviderPaymentId != receipt.ProviderPaymentId)
        {
            logger.LogWarning("ProviderPaymentId несоответствует для {OperationId}: stored={Stored}, received={Received}",
                receipt.OperationId, operation.ProviderPaymentId, receipt.ProviderPaymentId);
            
            throw new ConflictException(
                $"ProviderPaymentId несоответствует: {operation.ProviderPaymentId} vs {receipt.ProviderPaymentId}"
            );
        }

        // у операции не было ProviderPaymentId. ProviderPaymentId из квитанции сохраняется
        if (operation.ProviderPaymentId == null)
        {
            operation.ProviderPaymentId = receipt.ProviderPaymentId;
            await operationRepository.UpdateOperationAsync(operation);
            
            logger.LogInformation("Сохранение ProviderPaymentId {ProviderPaymentId} из квитанции в операцию {OperationId}", receipt.ProviderPaymentId, receipt.OperationId);
        }

        if (operation.Status == OperationStatus.COMPLETED || operation.Status == OperationStatus.REJECTED)
        {
            await eventRepository.AddEventAsync(new Event
            {
                OperationId = operation.OperationId,
                Type = EventType.IGNORED,
                FromStatus = operation.Status,
                ToStatus = operation.Status,
                Message = $"Ignored {receipt.Result} callback, already {operation.Status}",
                OccurredAt = DateTime.UtcNow,
                Operation = operation
            });
            
            logger.LogWarning("Квитанция игнорируется, так как операция уже в финальном статусе {Status}", operation.Status);
            return;
        }

        if (!Enum.TryParse<OperationStatus>(receipt.Result.ToUpper(), ignoreCase: true, out var newStatus))
        {
            throw new BadRequestException($"Недопустимый результат: {receipt.Result}");
        }

        if (newStatus != OperationStatus.COMPLETED && newStatus != OperationStatus.REJECTED)
        {
            throw new BadRequestException($"Ожидалось COMPLETED или REJECTED, но получен {receipt.Result}");
        }

        operation.Status = newStatus; 
        await operationRepository.UpdateOperationAsync(operation);
        
        var newEvent = new Event
        {
            OperationId = operation.OperationId,
            FromStatus = OperationStatus.PROCESSING,
            ToStatus = operation.Status,
            Message = receipt.Message,
            OccurredAt = DateTime.UtcNow,
            Operation = operation
        };

        newEvent.Type = receipt.Result.ToUpper() == nameof(EventType.COMPLETED) ? EventType.COMPLETED : EventType.REJECTED;
        
        await eventRepository.AddEventAsync(newEvent);
        
        logger.LogInformation("Операция {OperationId} получила статус {Result}", receipt.OperationId, receipt.Result);
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