namespace SportVitaal.Domain.DomainEvents
{
    public sealed class WaitingListJoinedEvent : IDomainEvent
    {
        public Guid LessonId { get; }
        public Guid UserId { get; }
        public DateTime JoinedAt { get; }

        public WaitingListJoinedEvent(Guid lessonId, Guid userId)
        {
            LessonId = lessonId;
            UserId = userId;
            JoinedAt = DateTime.UtcNow;
        }
    }
}

