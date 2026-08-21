using System.Text;
using System.Text.Json;
using TransactionService.Data.DTOs;

namespace TransactionService.Services;

public class ProviderService(ILogger<OperationService> logger, HttpClient httpClient) : IProviderService //пока заглушка
{
    public async Task<ProviderResponse> SendPaymentAsync(string operationId, string amount, string currency)
    {
        var requestBody = new ProviderRequest
        {
            OperationId = operationId,
            Amount = amount,
            Currency = currency
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, "/payments")
        {
            Content = content
        };
        
        request.Headers.Add("Idempotency-Key", operationId);
        request.Headers.Add("X-Correlation-ID", operationId);
        
        logger.LogInformation("Отправка платежа провайдеру. OperationId: {OperationId}, Idempotency-Key: {IdempotencyKey}", operationId, operationId);

        try
        {
            var response = await httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<ProviderResponse>(
                    responseContent, 
                    new JsonSerializerOptions{PropertyNameCaseInsensitive = true});

                logger.LogInformation(
                    "Провайдер принял платеж. OperationId: {OperationId}, ProviderPaymentId: {ProviderPaymentId}",
                    operationId, result?.ProviderPaymentId);
                
                return result ?? throw new Exception("Провайдер вернул null");;
            }

            if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
            {
                logger.LogWarning("ПРовайдер вернул код 503 для операции OperationId: {OperationId}", operationId);
                throw new HttpRequestException($"Провайдер недоступен: {response.StatusCode}");
            }
            
            logger.LogError("Провайдер вернул ошибку для операции OperationId: {OperationId}. Status: {StatusCode}, Response: {Response}",
                operationId, response.StatusCode, responseContent);
            
            throw new Exception($"Ошибка провайдера: {response.StatusCode}, {responseContent}");
        }
        
        catch (TaskCanceledException e)
        {
            logger.LogWarning(e, "Превышено время ожидания ответа от провайдера для операции OperationId: {OperationId}", operationId);
            throw new HttpRequestException("Таймаут от правайдера", e);
        }
        
        catch (Exception e)
        {
            logger.LogError(e, "Ошибка при вызове провайдера для OperationId: {OperationId}", operationId);
            throw;
        }
    }
} 