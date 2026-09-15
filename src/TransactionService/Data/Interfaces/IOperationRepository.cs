using TransactionService.Models;

namespace TransactionService.Data.Interfaces;

public interface IOperationRepository
{
    Task<Operation?> GetByOperationIdAsync(string operationId, CancellationToken cancellationToken = default);
    Task<Operation> CreateOperationAsync(Operation operation, CancellationToken cancellationToken = default);
    Task UpdateOperationAsync(Operation operation, CancellationToken cancellationToken = default);
    Task<bool> ExistsOperationAsync(string operationId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Operation>> GetProcessingOperationsAsync(CancellationToken cancellationToken);
    Task<bool> TryTransitionToProcessingAsync(string operationId, CancellationToken cancellationToken = default);
}