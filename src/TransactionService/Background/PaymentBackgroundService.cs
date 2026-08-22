using TransactionService.Data.Interfaces;
using TransactionService.Models;
using TransactionService.Models.Enums;
using TransactionService.Services;

namespace TransactionService.Background;

public class PaymentBackgroundService(IServiceScopeFactory scopeFactory, ILogger<PaymentBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while(!stoppingToken.IsCancellationRequested)
        {
            await ProcessPendingOperationsAsync();
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task ProcessPendingOperationsAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var operationRepository = scope.ServiceProvider.GetRequiredService<IOperationRepository>();
        var eventRepository = scope.ServiceProvider.GetRequiredService<IEventRepository>();
        var providerService = scope.ServiceProvider.GetRequiredService<IProviderService>();

        var pending = await operationRepository.GetProcessingOperationsAsync();

        foreach (var operation in pending)
        {
            logger.LogInformation("Подготовка к отправки запроса провайдеру на проведение операции {OperationId}", operation.OperationId);

            try
            {
                await eventRepository.AddEventAsync(new Event
                {
                    OperationId = operation.OperationId,
                    Type = EventType.PROVIDER_REQUEST,
                    FromStatus = OperationStatus.PROCESSING,
                    ToStatus = OperationStatus.PROCESSING,
                    Message = "Sending payment request to provider",
                    OccurredAt = DateTime.UtcNow,
                    Operation = operation
                });

                var response =
                    await providerService.SendPaymentAsync(operation.OperationId, operation.Amount, operation.Currency);

                await eventRepository.AddEventAsync(new Event
                {
                    OperationId = operation.OperationId,
                    Type = EventType.PROVIDER_RESPONSE_RECEIVED,
                    FromStatus = OperationStatus.PROCESSING,
                    ToStatus = OperationStatus.PROCESSING,
                    Message = $"Provider accepted payment: {response.ProviderPaymentId}",
                    OccurredAt = DateTime.UtcNow,
                    Operation = operation
                });

                operation.ProviderPaymentId = response.ProviderPaymentId;
                await operationRepository.UpdateOperationAsync(operation);

                logger.LogInformation("Провайдер успешно ответил для {OperationId}", operation.OperationId);
            }
            catch (HttpRequestException e) when (e.Message.Contains("503") || e.Message.Contains("unavailable"))
            {
                await eventRepository.AddEventAsync(new Event
                {
                    OperationId = operation.OperationId,
                    Type = EventType.PROVIDER_SERVICE_UNAVAILABLE,
                    FromStatus = OperationStatus.PROCESSING,
                    ToStatus = OperationStatus.PROCESSING,
                    Message = $"Provider unavailable (503): {e.Message}",
                    OccurredAt = DateTime.UtcNow,
                    Operation = operation
                });
                
                logger.LogWarning(e, "Провайдер недоступен для {OperationId}", operation.OperationId);
            }
            catch (HttpRequestException e)
            {
                await eventRepository.AddEventAsync(new Event
                {
                    OperationId = operation.OperationId,
                    Type = EventType.PROVIDER_NETWORK_ERROR,
                    FromStatus = OperationStatus.PROCESSING,
                    ToStatus = OperationStatus.PROCESSING,
                    Message = $"Network error: {e.Message}",
                    OccurredAt = DateTime.UtcNow,
                    Operation = operation
                });
                
                logger.LogWarning(e, "Сетевая ошибка для {OperationId}", operation.OperationId);
            }
            catch (Exception e)
            {
                await eventRepository.AddEventAsync(new Event
                {
                    OperationId = operation.OperationId,
                    Type = EventType.PROVIDER_UNKNOWN_ERROR,
                    FromStatus = OperationStatus.PROCESSING,
                    ToStatus = OperationStatus.PROCESSING,
                    Message = $"Unexpected error: {e.Message}",
                    OccurredAt = DateTime.UtcNow,
                    Operation = operation
                });

                logger.LogError(e,"Неизвестная ошибка для {OperationId}", operation.OperationId);
            }
        }
    }
}