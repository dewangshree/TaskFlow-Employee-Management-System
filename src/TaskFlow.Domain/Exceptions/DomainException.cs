namespace TaskFlow.Domain.Exceptions;

/// <summary>
/// Thrown when an operation would violate a business rule (for example an
/// invalid task status transition or reviewing an already processed leave).
/// Mapped to HTTP 409/422 by the API's global exception handler.
/// </summary>
public class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
