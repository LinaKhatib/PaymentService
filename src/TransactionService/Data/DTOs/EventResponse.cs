namespace TransactionService.Data.DTOs;

public class EventResponse
{
    public int EventId { get; set; } 
    public string Type { get; set; }  = string.Empty;
    public string? FromStatus { get; set; }  = null;
    public string ToStatus { get; set; }  = string.Empty;
    public string Message { get; set; }  = string.Empty;
    public string OccurredAt { get; set; }  = string.Empty;
}