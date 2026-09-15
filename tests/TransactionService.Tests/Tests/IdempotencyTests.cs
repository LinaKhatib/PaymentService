using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TransactionService.Data;
using TransactionService.Data.DTOs;
using TransactionService.Models.Enums;
using TransactionService.Tests.Factories;

namespace TransactionService.Tests.Tests;

public class IdempotencyTests
{
    [Fact]
    public async Task Receipt_DuplicateCallback_ProcessedOnlyOnce()
    {
        await using var factory = new TransactionServiceFactory
        {
            DisableBackgroundService = false 
        };
        await factory.InitializeAsync();

        factory.FakeProvider.ShouldFail = false;

        var client = factory.CreateClient();
        
        var operationId = $"idenpotency-{Guid.NewGuid()}";
        
        await client.PostAsJsonAsync("/operations", new
        {
            operationId,
            amount = "100.00",
            currency = "RUB",
            description = "Idempotency test"
        });
        
        await client.PostAsync($"/operations/{operationId}/submit", null);
        
        var timeout = TimeSpan.FromSeconds(20);
        var start = DateTime.UtcNow;
        while (factory.FakeProvider.CallCount == 0 && DateTime.UtcNow - start < timeout)
        {
            await Task.Delay(500);
        }
        Assert.True(factory.FakeProvider.CallCount > 0,"Провайдер не был вызван");

        var providerPaymentId = factory.FakeProvider.LastProviderPaymentId!;
        Assert.NotNull(providerPaymentId);
        
        var receipt = new
        {
            providerPaymentId,
            operationId,
            result = "COMPLETED",
            message = "Payment completed",
            occurredAt = DateTime.UtcNow
        };
        
        var firstResponse = await client.PostAsJsonAsync("/receipts", receipt);
        Assert.Equal(HttpStatusCode.NoContent, firstResponse.StatusCode);
        
        var secondResponse = await client.PostAsJsonAsync("/receipts", receipt);
        Assert.Equal(HttpStatusCode.NoContent, secondResponse.StatusCode);
        
        var operationResponse = await client.GetAsync($"/operations/{operationId}");
        var operation = await operationResponse.Content.ReadFromJsonAsync<OperationResponse>();
         
        Assert.NotNull(operation);
        Assert.Equal("COMPLETED", operation.Status);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

        var completedEvents = dbContext.Events
            .Where(e => e.OperationId == operationId && e.Type == EventType.COMPLETED)
            .ToList();
        
        Assert.Single(completedEvents);
    }
}