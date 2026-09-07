using Microsoft.EntityFrameworkCore;
using System.Linq;
using TransactionService.Data.Interfaces;
using TransactionService.Models;
using TransactionService.Models.Enums;

namespace TransactionService.Data.Repositories;

public class OperationRepository(PaymentDbContext context, ILogger<OperationRepository> logger) : IOperationRepository
{
    public async Task<Operation?> GetByOperationIdAsync(string operationId, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Запрос операции. {@OperationInfo}",
            new {OperationId = operationId});
        
        return await context.Operations
            .FirstOrDefaultAsync(o => o.OperationId == operationId, cancellationToken);
    }

    public async Task<Operation> CreateOperationAsync(Operation operation, CancellationToken cancellationToken = default)
    {
        logger.LogDebug(
            "Сохранение новой операции. {@OperationInfo}",
            new {operation.OperationId});
        
        await context.Operations.AddAsync(operation, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        
        return operation;
    }

    public async Task UpdateOperationAsync(Operation operation, CancellationToken cancellationToken = default)
    {
        logger.LogDebug(
            "Сохранение изменений операции. {@OperationInfo}",
            new { operation.OperationId });
        
        context.Operations.Update(operation);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsOperationAsync(string operationId, CancellationToken cancellationToken = default)
    {
        logger.LogDebug(
            "Запрос существования операции. {@OperationInfo}",
            new { OperationId = operationId });
        
        return await context.Operations
            .AnyAsync(o => o.OperationId == operationId, cancellationToken);
    }

    public async Task<IEnumerable<Operation>> GetProcessingOperationsAsync(CancellationToken cancellationToken)
    {
        logger.LogDebug("Поиск операций в статусе PROCESSING");
        
        return await context.Operations
            .Where(o => o.Status == OperationStatus.PROCESSING)
            .ToListAsync(cancellationToken);
    }
}