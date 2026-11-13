using TaskFlow.Domain.Common;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Events;

public sealed record LeaveRequestedEvent(
    int LeaveRequestId,
    string EmployeeId,
    LeaveType LeaveType,
    DateOnly StartDate,
    DateOnly EndDate) : DomainEvent;

public sealed record LeaveReviewedEvent(
    int LeaveRequestId,
    string EmployeeId,
    LeaveStatus Decision,
    string ReviewedById,
    DateOnly StartDate,
    DateOnly EndDate,
    string? AdminComment) : DomainEvent;
