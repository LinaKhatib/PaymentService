using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TransactionService.Data;
using TransactionService.Models;
using TransactionService.Models.Enums;
using TransactionService.Tests.Factories;

namespace TransactionService.Tests.Tests;

public class ConcurrentSubmitTests 
{
    [Fact]
    public async Task Submit_ConcurrentRequests_OnlyOneCreatesIntent()
    {
        await using var factory = new TransactionServiceFactory
        {
            DisableBackgroundService = true
        };
        
        await factory.InitializeAsync();

        var client = factory.CreateClient();

        var operationId = $"concurrent-{Guid.NewGuid()}";

        var createRequest = new
        {
            operationId,
            amount = "100.00",
            currency = "EUR",
            description = "Concurrent test",
        };

        var createResponse = await client.PostAsJsonAsync("/operations", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var tasks = Enumerable.Range(0, 5)
            .Select(_ => client.PostAsync($"/operations/{operationId}/submit", null))
            .ToList();

        var responses = await Task.WhenAll(tasks);
        
        var statusCode = responses.Select(r => (int)r.StatusCode).ToList();
        
        Assert.All(statusCode, code =>
                Assert.True(code == 200 || code == 202, $"Нежиданный код: {code}"));

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

        var operation = dbContext.Operations
            .FirstOrDefault(o => o.OperationId == operationId);
        
        Assert.NotNull(operation);
        Assert.Equal("PROCESSING", operation.Status.ToString());

        var submitEvents = dbContext.Events
            .Where(e => e.OperationId == operationId && e.Type == EventType.SUBMIT_ATTEMPT)
            .ToList<Event>();

        Assert.Single(submitEvents);
    }
}