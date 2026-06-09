namespace SportVitaal.Domain.DomainEvents
{
    public sealed class MembershipExpiringSoonEvent : IDomainEvent
    {
        public Guid MembershipId { get; }
        public Guid UserId { get; }
        public DateTime ExpiryDate { get; }
        public DateTime TriggeredAt { get; }

        public MembershipExpiringSoonEvent(Guid membershipId, Guid userId, DateTime expiryDate)
        {
            MembershipId = membershipId;
            UserId = userId;
            ExpiryDate = expiryDate;
            TriggeredAt = DateTime.UtcNow;
        }
    }
}

