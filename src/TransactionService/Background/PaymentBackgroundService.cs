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
        var eventFactory = scope.ServiceProvider.GetRequiredService<IEventFactory>();

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

                await eventRepository.AddEventAsync(eventFactory.CreateEventAsync(
                    operation,
                    EventType.FAILED,
                    $"Exceeded max retries ({MAX_RETRIES})",
                    toStatus: OperationStatus.FAILED), cancellationToken);
                
                continue;
            }
            
            try
            {
                await eventRepository.AddEventAsync(eventFactory.CreateEventAsync(
                    operation,
                    EventType.PROVIDER_REQUEST,
                    "Sending payment request to provider"), cancellationToken);
                
                var response = await providerService.SendPaymentAsync(operation.OperationId, operation.Amount, operation.Currency, cancellationToken);

                await eventRepository.AddEventAsync(eventFactory.CreateEventAsync(
                    operation,
                    EventType.PROVIDER_RESPONSE_RECEIVED,
                    $"Provider accepted payment: {response.ProviderPaymentId}"), cancellationToken);
               
                var freshOperation = await operationRepository.GetByOperationIdAsync(operation.OperationId, cancellationToken);
            
                if (freshOperation == null)
                    continue;
                
                if (!string.IsNullOrEmpty(freshOperation.ProviderPaymentId))
                {
                    logger.LogInformation("-- ProviderPaymentId уже установлен для {OperationId}: {ProviderPaymentId}, пропускаем", operation.OperationId, freshOperation.ProviderPaymentId);
                    if (freshOperation.ProviderPaymentId == response.ProviderPaymentId)
                    {
                        await eventRepository.AddEventAsync(eventFactory.CreateEventAsync(
                            operation,
                            EventType.LATE_PROVIDER_RESPONSE_RECEIVED,
                            $"A late response arrived from the provider: {response.ProviderPaymentId}"), cancellationToken);
                    }
                    else
                    {
                        await eventRepository.AddEventAsync(eventFactory.CreateEventAsync(
                            operation,
                            EventType.LATE_PROVIDER_RESPONSE_IGNORED,
                            $"A late response arrived from the provider with an incorrect ProviderPaymentId: {response.ProviderPaymentId}"), cancellationToken);
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
                await eventRepository.AddEventAsync(eventFactory.CreateEventAsync(
                    operation,
                    EventType.PROVIDER_SERVICE_UNAVAILABLE,
                    $"Provider unavailable (503): {e.Message}"), cancellationToken);
                
                operation.RetryCount++;
                await operationRepository.UpdateOperationAsync(operation, cancellationToken);
                
                logger.LogWarning(e, "-- Провайдер недоступен для {OperationId}", operation.OperationId);
            }
            catch (HttpRequestException e) when (e.Message.Contains("timeout") || e.Message.Contains("Timeout"))
            {
                await eventRepository.AddEventAsync(eventFactory.CreateEventAsync(
                    operation,
                    EventType.PROVIDER_TIMEOUT,
                    $"Provider timeout: {e.Message}"), cancellationToken);

                operation.RetryCount++;
                await operationRepository.UpdateOperationAsync(operation, cancellationToken);
                
                logger.LogWarning(e, "-- Таймаут для {OperationId}", operation.OperationId);
            }
            catch (HttpRequestException e)
            {
                await eventRepository.AddEventAsync(eventFactory.CreateEventAsync(
                    operation,
                    EventType.PROVIDER_NETWORK_ERROR,
                    $"Network error: {e.Message}"), cancellationToken);
                
                operation.RetryCount++;
                await operationRepository.UpdateOperationAsync(operation, cancellationToken);
                
                logger.LogWarning(e, "-- Сетевая ошибка для {OperationId}", operation.OperationId);
            }
            catch (Exception e)
            {
                await eventRepository.AddEventAsync(eventFactory.CreateEventAsync(
                    operation,
                    EventType.PROVIDER_UNKNOWN_ERROR,
                    $"Unexpected error: {e.Message}"), cancellationToken);
                
                operation.RetryCount++;
                await operationRepository.UpdateOperationAsync(operation, cancellationToken);
                
                logger.LogError(e,"-- Неизвестная ошибка для {OperationId}", operation.OperationId);
            }
        }
    }
}