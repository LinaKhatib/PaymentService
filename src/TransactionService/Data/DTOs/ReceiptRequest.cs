namespace TransactionService.Data.DTOs;

public class ReceiptRequest
{
    public string ProviderPaymentId { get; set; } = string.Empty;
    public string OperationId { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string OccurredAt { get; set; } = string.Empty;

}