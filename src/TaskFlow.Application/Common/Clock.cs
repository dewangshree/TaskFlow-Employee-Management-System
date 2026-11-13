namespace TaskFlow.Application.Common;

public static class TimeProviderExtensions
{
    public static DateTime UtcNow(this TimeProvider time) => time.GetUtcNow().UtcDateTime;

    public static DateOnly Today(this TimeProvider time) => DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
}
