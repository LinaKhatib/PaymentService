using TransactionService.Data.DTOs;

namespace TransactionService.Services;

public interface IOperationService
{
    Task<OperationResponse> CreateOperationAsync(OperationRequest request);
    Task SubmitOperationAsync(string operationId);
    Task<OperationResponse> GetOperationAsync(string operationId);
}