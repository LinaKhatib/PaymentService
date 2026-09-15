using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TransactionService.Data;
using TransactionService.Models;
using TransactionService.Models.Enums;
using TransactionService.Tests.Factories;
using OperationStatus = System.Buffers.OperationStatus;

namespace TransactionService.Tests.Tests;

public class ConcurrentSubmitTests : IClassFixture<TransactionServiceFactory>
{
    private readonly TransactionServiceFactory _factory;
    private readonly HttpClient _client;
    
    public ConcurrentSubmitTests(TransactionServiceFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Submit_ConcurrentRequests_OnlyOneCreatesIntent()
    {
        var operationId = $"concurrent-{Guid.NewGuid()}";

        var createRequest = new
        {
            operationId,
            amount = "100.00",
            currency = "EUR",
            description = "Concurrent test",
        };

        var createResponse = await _client.PostAsJsonAsync("/operations", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var tasks = Enumerable.Range(0, 5)
            .Select(_ => _client.PostAsync($"/operations/{operationId}/submit", null))
            .ToList();

        var responses = await Task.WhenAll(tasks);
        
        var statusCode = responses.Select(r => (int)r.StatusCode).ToList();
        
        Assert.All(statusCode, code =>
                Assert.True(code == 200 || code == 202, $"Нежиданный код: {code}"));

        using var scope = _factory.Services.CreateScope();
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