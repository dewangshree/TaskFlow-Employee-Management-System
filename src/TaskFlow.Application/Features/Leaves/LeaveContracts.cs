using FluentValidation;
using TaskFlow.Application.Common;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Features.Leaves;

public sealed record LeaveDto(
    int Id,
    string EmployeeId,
    string EmployeeName,
    string EmployeeDepartment,
    LeaveType LeaveType,
    DateOnly StartDate,
    DateOnly EndDate,
    int TotalDays,
    string Reason,
    LeaveStatus Status,
    DateTime AppliedOn,
    string? AdminComment,
    string? ReviewedByName,
    DateTime? ReviewedAt,
    uint Version);

public sealed record LeaveQuery : PagedQuery
{
    public LeaveStatus? Status { get; init; }
    public LeaveType? LeaveType { get; init; }
}

public sealed record ApplyLeaveRequest(LeaveType LeaveType, DateOnly StartDate, DateOnly EndDate, string Reason);

public enum LeaveDecision { Approved, Rejected }

public sealed record ReviewLeaveRequest(LeaveDecision Decision, string? Comment, uint Version);

public sealed class ApplyLeaveRequestValidator : AbstractValidator<ApplyLeaveRequest>
{
    public ApplyLeaveRequestValidator()
    {
        RuleFor(x => x.LeaveType).IsInEnum();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(LeaveRequest.ReasonMaxLength);
        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("End date must be on or after start date.");
        RuleFor(x => x)
            .Must(x => x.EndDate.DayNumber - x.StartDate.DayNumber < 90)
            .OverridePropertyName("endDate")
            .WithMessage("A single leave request cannot exceed 90 days.");
    }
}

public sealed class ReviewLeaveRequestValidator : AbstractValidator<ReviewLeaveRequest>
{
    public ReviewLeaveRequestValidator()
    {
        RuleFor(x => x.Decision).IsInEnum();
        RuleFor(x => x.Comment).MaximumLength(LeaveRequest.CommentMaxLength);
    }
}

public sealed class LeaveQueryValidator : AbstractValidator<LeaveQuery>
{
    private static readonly string[] SortFields = ["appliedOn", "startDate", "employee", "status"];

    public LeaveQueryValidator()
    {
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.SortBy)
            .Must(s => s is null || SortFields.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"SortBy must be one of: {string.Join(", ", SortFields)}.");
    }
}
