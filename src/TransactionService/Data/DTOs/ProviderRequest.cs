namespace TransactionService.Data.DTOs;

public class ProviderRequest
{
    public string OperationId { get; set; } = string.Empty;
    public string Amount { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
}