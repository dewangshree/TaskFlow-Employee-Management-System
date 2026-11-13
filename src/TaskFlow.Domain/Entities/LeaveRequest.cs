using TaskFlow.Domain.Common;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Events;
using TaskFlow.Domain.Exceptions;

namespace TaskFlow.Domain.Entities;

public class LeaveRequest : Entity
{
    public const int ReasonMaxLength = 1000;
    public const int CommentMaxLength = 500;

    private LeaveRequest() { } // EF Core

    public int Id { get; private set; }
    public string EmployeeId { get; private set; } = string.Empty;
    public ApplicationUser? Employee { get; private set; }

    public LeaveType LeaveType { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public LeaveStatus Status { get; private set; } = LeaveStatus.Pending;
    public DateTime AppliedOn { get; private set; }

    public string? AdminComment { get; private set; }
    public string? ReviewedById { get; private set; }
    public ApplicationUser? ReviewedBy { get; private set; }
    public DateTime? ReviewedAt { get; private set; }

    /// <summary>Optimistic concurrency token (mapped to PostgreSQL xmin).</summary>
    public uint Version { get; private set; }

    public int TotalDays => EndDate.DayNumber - StartDate.DayNumber + 1;

    public static LeaveRequest Submit(
        string employeeId,
        LeaveType type,
        DateOnly startDate,
        DateOnly endDate,
        string reason,
        DateTime nowUtc)
    {
        if (endDate < startDate)
            throw new DomainException("leave.invalid_range", "End date must be on or after start date.");

        var leave = new LeaveRequest
        {
            EmployeeId = employeeId,
            LeaveType = type,
            StartDate = startDate,
            EndDate = endDate,
            Reason = reason.Trim(),
            Status = LeaveStatus.Pending,
            AppliedOn = nowUtc,
        };

        leave.Raise(() => new LeaveRequestedEvent(leave.Id, employeeId, type, startDate, endDate));
        return leave;
    }

    public void Approve(string adminId, string? comment, DateTime nowUtc) =>
        Review(LeaveStatus.Approved, adminId, comment, nowUtc);

    public void Reject(string adminId, string? comment, DateTime nowUtc) =>
        Review(LeaveStatus.Rejected, adminId, comment, nowUtc);

    private void Review(LeaveStatus decision, string adminId, string? comment, DateTime nowUtc)
    {
        // Guards against duplicate approvals: a request can be decided exactly once.
        if (Status != LeaveStatus.Pending)
            throw new DomainException(
                "leave.already_reviewed",
                $"This leave request has already been {Status.ToString().ToLowerInvariant()}.");

        Status = decision;
        AdminComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        ReviewedById = adminId;
        ReviewedAt = nowUtc;

        var finalComment = AdminComment;
        Raise(() => new LeaveReviewedEvent(Id, EmployeeId, decision, adminId, StartDate, EndDate, finalComment));
    }

    /// <summary>Used by the seeder to recreate historical demo data.</summary>
    public static LeaveRequest Historical(
        string employeeId, LeaveType type, DateOnly start, DateOnly end, string reason,
        LeaveStatus status, DateTime appliedOn, string? comment, string? reviewedById)
    {
        return new LeaveRequest
        {
            EmployeeId = employeeId,
            LeaveType = type,
            StartDate = start,
            EndDate = end,
            Reason = reason,
            Status = status,
            AppliedOn = appliedOn,
            AdminComment = comment,
            ReviewedById = status == LeaveStatus.Pending ? null : reviewedById,
            ReviewedAt = status == LeaveStatus.Pending ? null : appliedOn.AddDays(1),
        };
    }
}
