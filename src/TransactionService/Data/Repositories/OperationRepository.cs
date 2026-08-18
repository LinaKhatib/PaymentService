using Microsoft.EntityFrameworkCore;
using System.Linq;
using TransactionService.Data.Interfaces;
using TransactionService.Models;
using TransactionService.Models.Enums;

namespace TransactionService.Data.Repositories;

public class OperationRepository(PaymentDbContext context, ILogger<OperationRepository> logger) : IOperationRepository
{
    public async Task<Operation?> GetByOperationIdAsync(string operationId)
    {
        logger.LogDebug("--- Запрос операции: {OperationId}", operationId);
        
        return await context.Operations
            .FirstOrDefaultAsync(o => o.OperationId == operationId);
    }

    public async Task<Operation> CreateOperationAsync(Operation operation)
    {
        logger.LogDebug("--- Сохранение новой операции: {OperationId}", operation.OperationId);
        
        await context.Operations.AddAsync(operation);
        await context.SaveChangesAsync();
        
        return operation;
    }

    public async Task UpdateOperationAsync(Operation operation)
    {
        logger.LogDebug("--- Сохранение изменений операции: {OperationId}", operation.OperationId);
        context.Operations.Update(operation);
        await context.SaveChangesAsync();
    }

    public async Task<bool> ExistsOperationAsync(string operationId)
    {
        logger.LogDebug("--- Запрос существования операции: {OperationId}", operationId);
        return await context.Operations
            .AnyAsync(o => o.OperationId == operationId);
    }

    public async Task<IEnumerable<Operation>> GetProcessingOperationsAsync()
    {
        logger.LogDebug("--- Поиск операций в статусе PROCESSING");
        return await context.Operations
            .Where(o => o.Status == OperationStatus.PROCESSING)
            .ToListAsync();
    }
}