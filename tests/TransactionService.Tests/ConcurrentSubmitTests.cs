// using System.Net.Http.Json;
// using Microsoft.AspNetCore.Mvc.Testing;
//
// namespace TransactionService.Tests;
//
// public class ConcurrentSubmitTests : IClassFixture<WebApplicationFactory<Program>>
// {
//     private readonly HttpClient _client;
//
//     public ConcurrentSubmitTests(WebApplicationFactory<Program> factory)
//     {
//         _client = factory.CreateClient();
//     }
//
//     [Fact]
//     public async Task Submit_ConcurrentRequests_OnlyOneCreatesIntent()
//     {
//         var operationId = Guid.NewGuid().ToString();
//         
//         await _client.PostAsJsonAsync("/operations", new
//         {
//             operationId,
//             amount = "100.00",
//             currency = "EUR",
//             description = "Test"
//         });
//         
//         var tasks = Enumerable.Range(0, 5).Select(_ => _client.PostAsync($"/operations/{operationId}/submit", null));
//         
//         var responses = await Task.WhenAll(tasks);
//
//         var ststusCodes = responses.Select(x => (int)x.StatusCode).ToList();
//         Assert.Contains(202,ststusCodes);
//     }
// }