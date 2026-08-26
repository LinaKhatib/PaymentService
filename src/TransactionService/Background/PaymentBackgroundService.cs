using TransactionService.Data.Interfaces;
using TransactionService.Models;
using TransactionService.Models.Enums;
using TransactionService.Services;

namespace TransactionService.Background;

public class PaymentBackgroundService(IServiceScopeFactory scopeFactory, ILogger<PaymentBackgroundService> logger) : BackgroundService
{
    private const int MAX_RETRIES = 3; 
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while(!stoppingToken.IsCancellationRequested)
        {
            await ProcessPendingOperationsAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task ProcessPendingOperationsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var operationRepository = scope.ServiceProvider.GetRequiredService<IOperationRepository>();
        var eventRepository = scope.ServiceProvider.GetRequiredService<IEventRepository>();
        var providerService = scope.ServiceProvider.GetRequiredService<IProviderService>();

        var pending = await operationRepository.GetProcessingOperationsAsync(cancellationToken);

        foreach (var operation in pending)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
            
            logger.LogInformation("-- Подготовка к отправки запроса провайдеру на проведение операции {OperationId}", operation.OperationId);

            if (operation.RetryCount >= MAX_RETRIES)
            {
                logger.LogWarning("-- Операция {OperationId} превысила максимальное количество попыток ({MaxRetries}), статус изменен на FAILED", operation.OperationId, MAX_RETRIES);
                operation.Status = OperationStatus.FAILED;
                await operationRepository.UpdateOperationAsync(operation, cancellationToken);
                
                await eventRepository.AddEventAsync(new Event
                {
                    OperationId = operation.OperationId,
                    Type = EventType.FAILED,
                    FromStatus = OperationStatus.PROCESSING,
                    ToStatus = OperationStatus.FAILED,
                    Message = $"Exceeded max retries ({MAX_RETRIES})",
                    OccurredAt = DateTime.UtcNow,
                    Operation = operation
                }, cancellationToken);
                
                continue;
            }
            
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
                }, cancellationToken);

                var response = await providerService.SendPaymentAsync(operation.OperationId, operation.Amount, operation.Currency, cancellationToken);

                await eventRepository.AddEventAsync(new Event
                {
                    OperationId = operation.OperationId,
                    Type = EventType.PROVIDER_RESPONSE_RECEIVED,
                    FromStatus = OperationStatus.PROCESSING,
                    ToStatus = OperationStatus.PROCESSING,
                    Message = $"Provider accepted payment: {response.ProviderPaymentId}",
                    OccurredAt = DateTime.UtcNow,
                    Operation = operation
                }, cancellationToken);
                
                var freshOperation = await operationRepository.GetByOperationIdAsync(operation.OperationId, cancellationToken);
            
                if (freshOperation == null)
                    continue;
                
                if (!string.IsNullOrEmpty(freshOperation.ProviderPaymentId))
                {
                    logger.LogInformation("-- ProviderPaymentId уже установлен для {OperationId}: {ProviderPaymentId}, пропускаем", operation.OperationId, freshOperation.ProviderPaymentId);
                    if (freshOperation.ProviderPaymentId == response.ProviderPaymentId)
                    {
                        await eventRepository.AddEventAsync(new Event
                        {
                            OperationId = operation.OperationId,
                            Type = EventType.LATE_PROVIDER_RESPONSE_RECEIVED,
                            FromStatus = OperationStatus.PROCESSING,
                            ToStatus = OperationStatus.PROCESSING,
                            Message = $"A late response arrived from the provider: {response.ProviderPaymentId}",
                            OccurredAt = DateTime.UtcNow,
                            Operation = operation
                        }, cancellationToken);
                    }
                    else
                    {
                        await eventRepository.AddEventAsync(new Event
                        {
                            OperationId = operation.OperationId,
                            Type = EventType.LATE_PROVIDER_RESPONSE_IGNORED,
                            FromStatus = OperationStatus.PROCESSING,
                            ToStatus = OperationStatus.PROCESSING,
                            Message = $"A late response arrived from the provider with an incorrect ProviderPaymentId: {response.ProviderPaymentId}",
                            OccurredAt = DateTime.UtcNow,
                            Operation = operation
                        }, cancellationToken);
                    }
                    continue;
                }
                
                if (freshOperation.Status == OperationStatus.COMPLETED || freshOperation.Status == OperationStatus.REJECTED) 
                {
                    logger.LogWarning("-- Операция {OperationId} уже в финальном статусе: {Status}, пропускаем", operation.OperationId, freshOperation.Status);
                    continue;
                }

                operation.RetryCount = 0;
                operation.ProviderPaymentId = response.ProviderPaymentId;
                await operationRepository.UpdateOperationAsync(operation, cancellationToken);

                logger.LogInformation("-- Провайдер успешно ответил для {OperationId}", operation.OperationId);
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
                }, cancellationToken);
                
                operation.RetryCount++;
                await operationRepository.UpdateOperationAsync(operation, cancellationToken);
                
                logger.LogWarning(e, "-- Провайдер недоступен для {OperationId}", operation.OperationId);
            }
            catch (HttpRequestException e) when (e.Message.Contains("timeout") || e.Message.Contains("Timeout"))
            {
                await eventRepository.AddEventAsync(new Event
                {
                    OperationId = operation.OperationId,
                    Type = EventType.PROVIDER_TIMEOUT,
                    FromStatus = OperationStatus.PROCESSING,
                    ToStatus = OperationStatus.PROCESSING,
                    Message = $"Provider timeout: {e.Message}",
                    OccurredAt = DateTime.UtcNow,
                    Operation = operation
                }, cancellationToken);
                
                operation.RetryCount++;
                await operationRepository.UpdateOperationAsync(operation, cancellationToken);
                
                logger.LogWarning(e, "-- Таймаут для {OperationId}", operation.OperationId);
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
                }, cancellationToken);
                
                operation.RetryCount++;
                await operationRepository.UpdateOperationAsync(operation, cancellationToken);
                
                logger.LogWarning(e, "-- Сетевая ошибка для {OperationId}", operation.OperationId);
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
                }, cancellationToken);
                
                operation.RetryCount++;
                await operationRepository.UpdateOperationAsync(operation, cancellationToken);
                
                logger.LogError(e,"-- Неизвестная ошибка для {OperationId}", operation.OperationId);
            }
        }
    }
}