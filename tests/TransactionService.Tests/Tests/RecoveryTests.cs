using System.Net;
using System.Net.Http.Json;
using TransactionService.Data.DTOs;
using TransactionService.Tests.Factories;

namespace TransactionService.Tests.Tests;

public class RecoveryTests
{
    [Fact]
    public async Task Submit_ProviderFailure_OperationStaysProcessing()
    {
        await using var factory = new TransactionServiceFactory
        {
            DisableBackgroundService = false 
        };
        await factory.InitializeAsync();

        factory.FakeProvider.ShouldFail = true;

        var client = factory.CreateClient();
        
        var operationId = $"recovery-{Guid.NewGuid()}";
        
        await client.PostAsJsonAsync("/operations", new
        {
            operationId,
            amount = "100.00",
            currency = "RUB",
            description = "Recovery test"
        });
        
        var submitResponse = await client.PostAsync(
            $"/operations/{operationId}/submit", null);

        Assert.Equal(HttpStatusCode.Accepted, submitResponse.StatusCode);
        
        var timeout = TimeSpan.FromSeconds(20);
        var start = DateTime.UtcNow;
        while (factory.FakeProvider.CallCount == 0 && DateTime.UtcNow - start < timeout)
        {
            await Task.Delay(500);
        }
        
        Assert.True(factory.FakeProvider.CallCount > 0, 
            $"CallCount = {factory.FakeProvider.CallCount}");


        
        var operationResponse = await client.GetAsync($"/operations/{operationId}");
        var operation = await operationResponse.Content.ReadFromJsonAsync<OperationResponse>();
        
        Assert.NotNull(operation);
        Assert.Equal("PROCESSING", operation.Status.ToString());
        Assert.Null(operation.ProviderPaymentId);

        Assert.True(factory.FakeProvider.CallCount > 0);
    }

    [Fact]
    public async Task Submit_ProviderRecovers_OperationCompletes()
    {
        await using var factory = new TransactionServiceFactory
        {
            DisableBackgroundService = false 
        };
        await factory.InitializeAsync();

        factory.FakeProvider.ShouldFail = false;

        var client = factory.CreateClient();
        
        var operationId = $"recovery-success-{Guid.NewGuid()}";
        await client.PostAsJsonAsync("/operations", new
        {
            operationId,
            amount = "100.00",
            currency = "RUB",
            description = "Recovery success test"
        });
        
        await client.PostAsync($"/operations/{operationId}/submit", null);
        
        var timeout = TimeSpan.FromSeconds(20);
        var start = DateTime.UtcNow;
        while (factory.FakeProvider.CallCount == 0 && DateTime.UtcNow - start < timeout)
        {
            await Task.Delay(500);
        }
        await Task.Delay(TimeSpan.FromSeconds(3));

        
        var operationResponse = await client.GetAsync($"/operations/{operationId}");
        var operation = await operationResponse.Content.ReadFromJsonAsync<OperationResponse>();
        
        Assert.NotNull(operation);
        Assert.Equal("PROCESSING", operation.Status);
        Assert.NotNull(operation.ProviderPaymentId);
    }
}