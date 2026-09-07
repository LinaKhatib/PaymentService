namespace TransactionService.Exceptions;

public class ProviderException : Exception
{
    public ProviderException() { }
    
    public ProviderException(string message) : base(message) { }
}