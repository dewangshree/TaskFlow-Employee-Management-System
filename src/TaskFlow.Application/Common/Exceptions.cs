namespace TaskFlow.Application.Common;

public sealed class NotFoundException(string resource, object key)
    : Exception($"{resource} '{key}' was not found.")
{
    public string Resource { get; } = resource;
}

public sealed class ForbiddenAccessException(string message = "You do not have permission to perform this action.")
    : Exception(message);

public sealed class ConflictException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class RequestValidationException(IDictionary<string, string[]> errors)
    : Exception("One or more validation errors occurred.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;

    public RequestValidationException(string field, string error)
        : this(new Dictionary<string, string[]> { [field] = [error] }) { }
}
