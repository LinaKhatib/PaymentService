using TransactionService.Data.DTOs;

namespace TransactionService.Services;

public interface IOperationService
{
    Task<OperationResponse> CreateOperationAsync(OperationRequest request);
    Task<(OperationResponse, bool StatusChanged)> SubmitOperationAsync(string operationId);
    Task<OperationResponse> GetOperationAsync(string operationId);
}