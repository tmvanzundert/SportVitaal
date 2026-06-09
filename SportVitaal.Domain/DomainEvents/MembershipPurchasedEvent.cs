namespace SportVitaal.Domain.DomainEvents
{
    public sealed class MembershipPurchasedEvent : IDomainEvent
    {
        public Guid MembershipId { get; }
        public Guid UserId { get; }
        public DateTime PurchasedAt { get; }

        public MembershipPurchasedEvent(Guid membershipId, Guid userId)
        {
            MembershipId = membershipId;
            UserId = userId;
            PurchasedAt = DateTime.UtcNow;
        }
    }
}

