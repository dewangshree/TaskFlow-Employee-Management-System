using System.Text.Json;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TaskFlow.Application.Abstractions;
using TaskFlow.Application.Events;
using TaskFlow.Domain.Common;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Messaging;
using TaskFlow.Infrastructure.Observability;

namespace TaskFlow.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser>(options), IApplicationDbContext
{
    public DbSet<TaskItem> TaskItems => Set<TaskItem>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasPostgresExtension("pg_trgm");
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public void SetExpectedVersion<TEntity>(TEntity entity, uint version) where TEntity : class =>
        Entry(entity).Property("Version").OriginalValue = version;

    /// <summary>
    /// Transactional outbox: domain events raised by aggregates are converted to
    /// OutboxMessage rows and committed in the SAME transaction as the business
    /// change. Either both are persisted or neither is, so no event is ever lost
    /// (or published for a change that was rolled back).
    /// </summary>
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var aggregates = ChangeTracker.Entries<IHasDomainEvents>()
            .Where(e => e.Entity.HasDomainEvents)
            .Select(e => e.Entity)
            .ToList();

        if (aggregates.Count == 0)
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

        IDbContextTransaction? ownedTransaction = Database.CurrentTransaction is null
            ? await Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            // First pass persists the aggregates so identity keys exist for event payloads.
            var affected = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

            foreach (var aggregate in aggregates)
            {
                foreach (var domainEvent in aggregate.CollectDomainEvents())
                    OutboxMessages.Add(ToOutboxMessage(domainEvent));
                aggregate.ClearDomainEvents();
            }

            affected += await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

            if (ownedTransaction is not null)
                await ownedTransaction.CommitAsync(cancellationToken);

            return affected;
        }
        finally
        {
            if (ownedTransaction is not null)
                await ownedTransaction.DisposeAsync();
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        ChangeTracker.Entries<IHasDomainEvents>().Any(e => e.Entity.HasDomainEvents)
            ? throw new InvalidOperationException("Use SaveChangesAsync for aggregates that raise domain events.")
            : base.SaveChanges(acceptAllChangesOnSuccess);

    private static OutboxMessage ToOutboxMessage(IDomainEvent domainEvent) => new()
    {
        Id = domainEvent.EventId,
        Type = IntegrationEventCatalog.TypeNameFor(domainEvent),
        RoutingKey = IntegrationEventCatalog.RoutingKeyFor(domainEvent),
        Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), MessagingJson.Options),
        CorrelationId = CorrelationContext.CorrelationId,
        OccurredAt = domainEvent.OccurredAtUtc,
    };
}
