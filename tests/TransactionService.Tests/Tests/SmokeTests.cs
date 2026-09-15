using TransactionService.Tests.Factories;

namespace TransactionService.Tests.Tests;

public class SmokeTests : IClassFixture<TransactionServiceFactory>
{
    private readonly HttpClient _client;

    public SmokeTests(TransactionServiceFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("healthy", content);
    }
}