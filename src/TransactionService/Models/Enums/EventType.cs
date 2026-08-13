namespace TransactionService.Models.Enums;

public enum EventType
{
    // сервесные
    CREATED,   // транзакция создана
    SUBMIT_ATTEMPT,   // запланирована отправка провайдеру
    PROVIDER_TIMEOUT,   // таймайт ответа от провайдера
    
    // после http-ответа от провайдера
    PROVIDER_RESPONSE_RECEIVED,   // успешный ответ от провайдера 
    PROVIDER_SERVICE_UNAVAILABLE,   // ошибка от провайдера (503)
    PROVIDER_NETWORK_ERROR,   // сетевая ошибка от провайдера
    LATE_PROVIDER_RESPONSE_RECEIVED,   // поздний ответ от провайдера (после callback) с совпадающим ID 
    LATE_PROVIDER_RESPONSE_IGNORED,   // поздний ответ от провайдера (после callback) с несовпадающим ID
    
    // после callback
    COMPLETED,   // callback с успехом (финальный статус)
    REJECTED,   // callback с отказом (финальный статус)
    CALLBACK_CONFLICT,   // callback с неверным ProviderPaymentId (409)
    CALLBACK_DUPLICATE   // поздний callback (операция уже финальная)
}