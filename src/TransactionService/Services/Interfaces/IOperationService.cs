using TransactionService.Data.DTOs;

namespace TransactionService.Services;

public interface IOperationService
{
    Task<OperationResponse> CreateOperationAsync(OperationRequest request, CancellationToken cancellationToken = default);
    Task<(OperationResponse, bool StatusChanged)> SubmitOperationAsync(string operationId, CancellationToken cancellationToken = default);
    Task<OperationResponse> GetOperationAsync(string operationId, CancellationToken cancellationToken = default);
    Task HandleReceiptAsync(ReceiptRequest receipt, CancellationToken cancellationToken = default);
}