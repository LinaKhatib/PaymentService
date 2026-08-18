using TransactionService.Data.DTOs;

namespace TransactionService.Services;

public class ProviderService(ILogger<OperationService> logger) : IProviderService //пока заглушка
{
    public Task<ProviderResponse> SendPaymentAsync(string operationId, string amount, string currency)
    {
        logger.LogInformation("[MOCK] Отправка запроса провайдеру: {OperationId}, сумма: {Amount}", operationId, amount);
        
        return Task.FromResult(
            new ProviderResponse 
            {
                ProviderPaymentId = $"mock-{Guid.NewGuid()}",
                Status = $"mock-{operationId}"
            });
    }
} 