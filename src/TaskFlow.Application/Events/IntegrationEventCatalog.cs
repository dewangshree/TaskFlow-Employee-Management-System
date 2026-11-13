using TaskFlow.Domain.Common;
using TaskFlow.Domain.Events;

namespace TaskFlow.Application.Events;

/// <summary>
/// Maps domain events to RabbitMQ routing keys and back to CLR types.
/// Topic routing keys ("task.*", "leave.*") let future consumers subscribe
/// to subsets of events without changing publishers.
/// </summary>
public static class IntegrationEventCatalog
{
    private static readonly Dictionary<Type, string> RoutingKeys = new()
    {
        [typeof(TaskAssignedEvent)] = "task.assigned",
        [typeof(TaskStatusChangedEvent)] = "task.status_changed",
        [typeof(LeaveRequestedEvent)] = "leave.requested",
        [typeof(LeaveReviewedEvent)] = "leave.reviewed",
    };

    private static readonly Dictionary<string, Type> TypesByName =
        RoutingKeys.Keys.ToDictionary(t => t.Name, t => t);

    public static string RoutingKeyFor(IDomainEvent domainEvent) =>
        RoutingKeys.TryGetValue(domainEvent.GetType(), out var key)
            ? key
            : throw new InvalidOperationException($"No routing key registered for {domainEvent.GetType().Name}.");

    public static string TypeNameFor(IDomainEvent domainEvent) => domainEvent.GetType().Name;

    public static Type? ResolveType(string typeName) => TypesByName.GetValueOrDefault(typeName);

    public static IEnumerable<string> AllRoutingKeys => RoutingKeys.Values;
}
