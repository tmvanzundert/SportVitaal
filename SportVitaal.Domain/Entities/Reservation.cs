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
        // EF needs a parameterless constructor; keep setters private to enforce invariants.
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
            // Free the seat so it can be re-booked. The unique (LessonId, SeatNumber) index
            // counts a non-null seat even on a cancelled row, so leaving it set would block
            // anyone else from taking that seat again. Multiple NULLs are allowed by the index.
            SeatNumber = null;
        }

        public void MarkAttended()
        {
            Status = ReservationStatus.Attended;
        }
    }
}


