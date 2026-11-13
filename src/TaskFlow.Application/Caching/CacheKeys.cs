namespace TaskFlow.Application.Caching;

/// <summary>
/// Centralised cache key scheme. User-scoped data always embeds the user id so one
/// user's cached data can never be served to another user.
/// The version segment allows invalidating every key at once when a DTO shape changes.
/// </summary>
public static class CacheKeys
{
    private const string Prefix = "taskflow:v1:";

    public const string AdminDashboard = Prefix + "dashboard:admin";
    public const string Departments = Prefix + "ref:departments";
    public const string AssignableEmployees = Prefix + "ref:assignable-employees";

    public static string EmployeeDashboard(string userId) => $"{Prefix}dashboard:employee:{userId}";
    public static string UnreadNotificationCount(string userId) => $"{Prefix}notifications:unread:{userId}";
}

public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    public int DashboardTtlSeconds { get; set; } = 120;
    public int ReferenceDataTtlSeconds { get; set; } = 600;
    public int NotificationCountTtlSeconds { get; set; } = 300;

    public TimeSpan DashboardTtl => TimeSpan.FromSeconds(DashboardTtlSeconds);
    public TimeSpan ReferenceDataTtl => TimeSpan.FromSeconds(ReferenceDataTtlSeconds);
    public TimeSpan NotificationCountTtl => TimeSpan.FromSeconds(NotificationCountTtlSeconds);
}
