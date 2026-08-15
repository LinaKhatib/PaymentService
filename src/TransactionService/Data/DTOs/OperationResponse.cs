namespace TransactionService.Data.DTOs;

public class OperationResponse
{
    public string OperationId { get; set; } = string.Empty;
    public string Amount { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ProviderPaymentId { get; set; } = null;
}