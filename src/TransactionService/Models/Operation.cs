using TransactionService.Models.Enums;

namespace TransactionService.Models;

public class Operation
{
    public string OperationId { get; set; } = string.Empty;
    public string Amount { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public OperationStatus Status { get; set; } = OperationStatus.CREATED;
    public string? ProviderPaymentId { get; set; } = null;
    
    public List<Event> Events { get; set; } = new();
}