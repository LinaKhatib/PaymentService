using TransactionService.Models.Enums;

namespace TransactionService.Models;

public class Event
{
    public int EventId { get; set; }
    public EventType Type { get; set; }
    
    public OperationStatus? FromStatus  { get; set; }
    public OperationStatus ToStatus { get; set; }

    public string Message { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    
    public string OperationId { get; set; } = string.Empty;
    public Operation Operation { get; set; } = new();
}