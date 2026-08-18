using TransactionService.Data.DTOs;

namespace TransactionService.Services;

public interface IProviderService
{
    Task<ProviderResponse> SendPaymentAsync(string operationId, string amount, string currency);
}