using FluentValidation;
using TaskFlow.Application.Common;

namespace TaskFlow.Application.Features.Employees;

public sealed record EmployeeDto(
    string Id,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    string Department,
    DateTime JoinDate,
    bool IsActive,
    int TotalTasks,
    int OpenTasks);

public sealed record EmployeeLookupDto(string Id, string FullName, string Department);

public sealed record EmployeeQuery : PagedQuery
{
    public string? Department { get; init; }
    public bool? IsActive { get; init; }
}

public sealed record SetEmployeeActiveRequest(bool IsActive);

public sealed class EmployeeQueryValidator : AbstractValidator<EmployeeQuery>
{
    private static readonly string[] SortFields = ["name", "email", "department", "joinDate", "openTasks"];

    public EmployeeQueryValidator()
    {
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.Department).MaximumLength(100);
        RuleFor(x => x.SortBy)
            .Must(s => s is null || SortFields.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"SortBy must be one of: {string.Join(", ", SortFields)}.");
    }
}
