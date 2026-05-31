namespace SportVitaal.Domain.DomainEvents
{
    public class ReservationPromotedEvent : IDomainEvent
    {
        public Guid LessonId { get; }
        public Guid MemberId { get; }
        public Guid ReservationId { get; }
        public DateTime OccurredAt { get; }

        public ReservationPromotedEvent(Guid lessonId, Guid memberId, Guid reservationId, DateTime occurredAt)
        {
            LessonId = lessonId;
            MemberId = memberId;
            ReservationId = reservationId;
            OccurredAt = occurredAt;
        }
    }
}

