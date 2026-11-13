using TaskFlow.Domain.Common;

namespace TaskFlow.Application.Abstractions;

public interface ICurrentUser
{
    string? UserId { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string role);

    bool IsAdmin => IsInRole(RoleNames.Admin);

    /// <summary>User id of the authenticated caller; throws if unauthenticated.</summary>
    string RequiredUserId => UserId ?? throw new UnauthorizedAccessException("Authentication is required.");
}
