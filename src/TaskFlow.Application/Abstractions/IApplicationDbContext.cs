using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Abstractions;

/// <summary>
/// Application-facing view of the database. Services compose LINQ queries
/// directly against DbSets (EF Core already implements Repository/Unit of Work),
/// while the provider-specific configuration stays in Infrastructure.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<ApplicationUser> Users { get; }
    DbSet<IdentityRole> Roles { get; }
    DbSet<IdentityUserRole<string>> UserRoles { get; }
    DbSet<TaskItem> TaskItems { get; }
    DbSet<LeaveRequest> LeaveRequests { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }
    DbSet<ProcessedMessage> ProcessedMessages { get; }

    /// <summary>
    /// Sets the concurrency token the client last saw. If the row changed since,
    /// SaveChanges throws DbUpdateConcurrencyException (mapped to HTTP 409).
    /// </summary>
    void SetExpectedVersion<TEntity>(TEntity entity, uint version) where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
