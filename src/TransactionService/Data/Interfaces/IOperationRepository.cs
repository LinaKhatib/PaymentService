using TransactionService.Models;

namespace TransactionService.Data.Interfaces;

public interface IOperationRepository
{
    Task<Operation?> GetByOperationIdAsync(string operationId);
    Task<Operation> CreateOperationAsync(Operation operation);
    Task UpdateOperationAsync(Operation operation);
    Task<bool> ExistsOperationAsync(string operationId);
    Task<IEnumerable<Operation>> GetProcessingOperationsAsync();
}