using System.Text;
using System.Text.Json;
using TransactionService.Data.DTOs;
using TransactionService.Exceptions;

namespace TransactionService.Services;

public class ProviderService(ILogger<OperationService> logger, HttpClient httpClient) : IProviderService
{
    public async Task<ProviderResponse> SendPaymentAsync(string operationId, string amount, string currency, CancellationToken cancellationToken = default)
    {
        const int maxRetries = 3;
        const int baseDalayMs = 1000;
        var retryCount = 0;
        var random = new Random();

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


        while (retryCount < maxRetries)
        {
            try
            {
                logger.LogInformation(
                    "Отправка операции провайдеру. {@RequestInfo}", 
                    new { OperationId = operationId, Attempt = retryCount + 1, MaxRetries = maxRetries });
                
                var response = await httpClient.SendAsync(request, cancellationToken);
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<ProviderResponse>(
                        responseContent,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    logger.LogInformation(
                        "Провайдер принял операцию и вернул ProviderPaymentId. {@ResponsInfo}",
                        new {OperationId = operationId, result?.ProviderPaymentId});

                    return result ?? throw new Exception("Провайдер вернул null");
                }

                if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                {
                    logger.LogWarning(
                        "Провайдер вернул 503 по операции. {@ResponsInfo}",
                        new {OperationId = operationId, Attempt = retryCount + 1, MaxRetries = maxRetries});

                    throw new HttpRequestException($"Провайдер недоступен: {response.StatusCode}");
                }

                logger.LogError(
                    "Провайдер вернул ошибку по операции. {@ResponsInfo}",
                    new {OperationId = operationId, response.StatusCode, ResponseContent = responseContent});

                throw new ProviderException($"Ошибка провайдера: {response.StatusCode}, {responseContent}");
            }

            catch (TaskCanceledException e) when (e.InnerException is TimeoutException)
            {
                logger.LogWarning(e,
                    "Таймаут для операции. {@OperationInfo}",
                    new {OperationId = operationId, Attempt = retryCount + 1, MaxRetries = maxRetries});
            }
            
            catch (TaskCanceledException e) when (e.Message.Contains("503") || e.Message.Contains("unavailable"))
            {
                logger.LogWarning(e,
                    "Провайдер недоступен для операций. {@OperationInfo}",
                    new {OperationId = operationId, Attempt = retryCount + 1, MaxRetries = maxRetries});
            }
            
            catch (Exception e)
            {
                logger.LogError(e, 
                    "Ошибка при вызове провайдера для операции. {@OperationInfo}", 
                    new {OperationId = operationId});
                throw;
            } 
            
            retryCount++;
                
            if (retryCount >= maxRetries)
            {
                logger.LogError(
                    "Все попытки к провайдеру провалились для операции. {@OperationInfo}", 
                    new {OperationId = operationId, MaxRetries = maxRetries});

                throw new ProviderException($"Не удалось отправить платеж {operationId} после {maxRetries} попыток");
            }

            var jitter = random.Next(0, 200);
            var delayMs = (int)(baseDalayMs * Math.Pow(2, retryCount - 1) + jitter);

            logger.LogInformation(
                "Ожидание {DelayMs}мс перед следующей попыткой", delayMs);

            await Task.Delay(delayMs, cancellationToken);
        }
        
        logger.LogError(
            "Не удалось отправить операцию провайдеру после всех попыток. {@OperationInfo}", 
            new {OperationId = operationId, MaxRetries = maxRetries});
        
        throw new InvalidOperationException($"Не удалось отправить платеж {operationId} после {maxRetries} попыток");
    }
} 