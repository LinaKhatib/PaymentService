using Microsoft.EntityFrameworkCore;
using System.Linq;
using TransactionService.Data.Interfaces;
using TransactionService.Models;
using TransactionService.Models.Enums;

namespace TransactionService.Data.Repositories;

public class OperationRepository(PaymentDbContext context) : IOperationRepository
{
    public async Task<Operation?> GetByOperationIdAsync(string operationId)
    {
        return await context.Operations
            .FirstOrDefaultAsync(o => o.OperationId == operationId);
        
    }

    public async Task<Operation> CreateOperationAsync(Operation operation)
    {
        await context.Operations.AddAsync(operation);
        await context.SaveChangesAsync();
        
        return operation;
    }

    public async Task UpdateOperationAsync(Operation operation)
    {
        context.Operations.Update(operation);
        await context.SaveChangesAsync();
    }

    public async Task<bool> ExistsOperationAsync(string operationId)
    {
        return await context.Operations
            .AnyAsync(o => o.OperationId == operationId);
    }

    public async Task<IEnumerable<Operation>> GetProcessingOperationsAsync()
    {
        return await context.Operations
            .Where(o => o.Status == OperationStatus.PROCESSING)
            .ToListAsync();
    }
}