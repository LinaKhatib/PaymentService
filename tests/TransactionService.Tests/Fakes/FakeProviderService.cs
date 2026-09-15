using TransactionService.Data.DTOs;
using TransactionService.Services;

namespace TransactionService.Tests.Fakes;

public class FakeProviderService : IProviderService
{
    public bool ShouldFail { get; set; } = true;
    public int CallCount { get; private  set; }
    
    public string? LastProviderPaymentId { get; private set; }
    public Task<ProviderResponse> SendPaymentAsync(string operationId, string amount, string currency,
        CancellationToken cancellationToken = default)
    {
        CallCount++;

        if (ShouldFail)
        {
            throw new HttpRequestException("Service Unavailable (503)");
        }

        LastProviderPaymentId = $"fake-{Guid.NewGuid()}";
        
        return Task.FromResult(new ProviderResponse
            {
                ProviderPaymentId = LastProviderPaymentId,
                Status = "ACCEPTED"
            });
    }
}