namespace SportVitaal.Domain.DomainEvents
{
    public sealed class ReservationCancelledEvent : IDomainEvent
    {
        public Guid ReservationId { get; }
        public Guid LessonId { get; }
        public Guid UserId { get; }
        public Guid CancelledBy { get; }
        public DateTime CancelledAt { get; }

        public ReservationCancelledEvent(Guid reservationId, Guid lessonId, Guid userId, Guid cancelledBy)
        {
            ReservationId = reservationId;
            LessonId = lessonId;
            UserId = userId;
            CancelledBy = cancelledBy;
            CancelledAt = DateTime.UtcNow;
        }
    }
}

