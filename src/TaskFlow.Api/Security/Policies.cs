namespace TaskFlow.Api.Security;

public static class Policies
{
    public const string AdminOnly = nameof(AdminOnly);
    public const string EmployeeOnly = nameof(EmployeeOnly);
}
