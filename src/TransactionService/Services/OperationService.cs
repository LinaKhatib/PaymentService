using TransactionService.Data.DTOs;
using TransactionService.Data.Interfaces;
using TransactionService.Models;
using TransactionService.Models.Enums;

namespace TransactionService.Services;

public class OperationService(IOperationRepository operationRepository, IEventRepository eventRepository) : IOperationService
{
    public async Task<OperationResponse> CreateOperationAsync(OperationRequest request)
    {
        if (await operationRepository.ExistsOperationAsync(request.OperationId))
        {
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
        
        var newEvent = new Event
        {
            OperationId = newOperation.OperationId,
            Type = EventType.CREATED,
            ToStatus = newOperation.Status,
            Message = "Operation created",
            OccurredAt =  DateTime.UtcNow
        };
        
        await eventRepository.AddEventAsync(newEvent);

        var responseOperation = new OperationResponse
        {
            OperationId = newOperation.OperationId,
            Amount = newOperation.Amount,
            Currency = newOperation.Currency,
            Description = newOperation.Description,
            Status = newOperation.Status.ToString(),
            ProviderPaymentId = newOperation.ProviderPaymentId
        };
        
        return responseOperation;
    }

    public async Task SubmitOperationAsync(string operationId)
    {
        throw new NotImplementedException();
    }

    public async Task<OperationResponse> GetOperationAsync(string operationId)
    {
        var operation = await operationRepository.GetByOperationIdAsync(operationId);

        if (operation == null)
        {
            throw new KeyNotFoundException($"Операция {operationId} не найдена.");
        }

        var responseOperation = new OperationResponse
        {
            OperationId = operation.OperationId,
            Amount = operation.Amount,
            Currency = operation.Currency,
            Description = operation.Description,
            Status = operation.Status.ToString(),
            ProviderPaymentId = operation.ProviderPaymentId
        };
        
        return responseOperation;
    }
}