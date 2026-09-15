using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TransactionService.Data;
using TransactionService.Data.DTOs;
using TransactionService.Data.Repositories;
using TransactionService.Models.Enums;
using TransactionService.Tests.Factories;

namespace TransactionService.Tests.Tests;

public class LateCallbackTests
{
    [Fact]
    public async Task Receipt_ConflictingLateCallback_IgnoredWith204()
    {
        await using var factory = new TransactionServiceFactory
        {
            DisableBackgroundService = false 
        };
        await factory.InitializeAsync();

        factory.FakeProvider.ShouldFail = false;

        var client = factory.CreateClient();

        var operationId = $"late-callback-{Guid.NewGuid()}";
        
        var createResponse = await client.PostAsJsonAsync("/operations", new
        {
            operationId,
            amount = "100.00",
            currency = "RUB",
            description = "Idempotency test"
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        
        var submitResponse = await client.PostAsync($"/operations/{operationId}/submit", null);
        Assert.Equal(HttpStatusCode.Accepted, submitResponse.StatusCode);

        var timeout = TimeSpan.FromSeconds(20);
        var start = DateTime.UtcNow;
        while (factory.FakeProvider.CallCount == 0 && DateTime.UtcNow - start < timeout)
        {
            await Task.Delay(500);
        }
        Assert.True(factory.FakeProvider.CallCount > 0,"Провайдер не был вызван");

        var providerPaymentId = factory.FakeProvider.LastProviderPaymentId!;
        Assert.NotNull(providerPaymentId);

        var completedReceipt = new
        {
            providerPaymentId,
            operationId,
            result = "COMPLETED",
            message = "Payment completed",
            occurredAt = DateTime.UtcNow
        };

        var completedResponse = await client.PostAsJsonAsync("/receipts", completedReceipt);
        Assert.Equal(HttpStatusCode.NoContent, completedResponse.StatusCode);
        
        var rejectedReceipt = new
        {
            providerPaymentId,
            operationId,
            result = "REJECTED",
            message = "Payment rejected late",
            occurredAt = DateTime.UtcNow.AddSeconds(10)
        };

        var rejectedResponse = await client.PostAsJsonAsync("/receipts", rejectedReceipt);
        Assert.Equal(HttpStatusCode.NoContent, rejectedResponse.StatusCode);
        
        var operationResponse = await client.GetAsync($"/operations/{operationId}");
            
        var operation  = await operationResponse.Content
            .ReadFromJsonAsync<OperationResponse>();

        Assert.NotNull(operation);
        Assert.Equal("COMPLETED", operation.Status);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

        var rejectedEvents = dbContext.Events
            .Where(e => e.OperationId == operationId && e.Type == EventType.REJECTED)
            .ToList();
        
        Assert.Empty(rejectedEvents);

        var completedEvents = dbContext.Events
            .Where(e => e.OperationId == operationId && e.Type == EventType.COMPLETED)
            .ToList();
        
        Assert.Single(completedEvents);
    }
}