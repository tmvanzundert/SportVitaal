namespace SportVitaal.Domain.DomainEvents
{
    public sealed class ReservationCreatedEvent : IDomainEvent
    {
        public Guid ReservationId { get; }
        public Guid LessonId { get; }
        public Guid UserId { get; }
        public DateTime CreatedAt { get; }

        public ReservationCreatedEvent(Guid reservationId, Guid lessonId, Guid userId)
        {
            ReservationId = reservationId;
            LessonId = lessonId;
            UserId = userId;
            CreatedAt = DateTime.UtcNow;
        }
    }
}

