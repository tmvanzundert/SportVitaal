namespace SportVitaal.Domain.Entities
{
    public enum ReservationStatus
    {
        Reserved,
        Cancelled,
        Attended
    }

    public class Reservation : BaseEntity
    {
        // This fixes the database not being able to process the attributes via Pomelo.
        // We want to keep the properties private set to enforce invariants,
        // but EF needs a parameterless constructor.
        protected Reservation() { }
        public Guid LessonId { get; private set; }
        public Guid MemberId { get; private set; }
        public int? SeatNumber { get; private set; }
        public ReservationStatus Status { get; private set; }

        public Reservation(Guid lessonId, Guid memberId, int? seatNumber = null)
        {
            LessonId = lessonId;
            MemberId = memberId;
            SeatNumber = seatNumber;
            Status = ReservationStatus.Reserved;
        }

        public void Cancel()
        {
            Status = ReservationStatus.Cancelled;
        }

        public void MarkAttended()
        {
            Status = ReservationStatus.Attended;
        }
    }
}


