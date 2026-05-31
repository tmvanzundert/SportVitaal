namespace SportVitaal.Domain.DomainEvents
{
    /// <summary>
    /// Simple dispatcher interface; infrastructure/application layer should implement
    /// and dispatch domain events produced by aggregates.
    /// </summary>
    public interface IDomainEventDispatcher
    {
        Task DispatchAsync(IDomainEvent domainEvent);
    }
}

