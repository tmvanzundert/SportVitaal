using SportVitaal.Domain.DomainExceptions;
using SportVitaal.Domain.DomainEvents;

namespace SportVitaal.Domain.Entities
{
    public class Lesson : BaseEntity
    {
        // EF needs a parameterless constructor; keep setters private to enforce invariants.
        protected Lesson() { Location = null!; }

        public Guid WorkoutId { get; private set; }
        public DateTime StartAt { get; private set; }
        public int DurationMinutes { get; private set; }
        public Location Location { get; private set; }
        public Guid? InstructorId { get; private set; }

        private readonly List<Reservation> _reservations = new();
        public IReadOnlyCollection<Reservation> Reservations => _reservations.AsReadOnly();

        // Domain events produced by this aggregate (collected for dispatch by application layer)
        private readonly List<IDomainEvent> _domainEvents = new();
        public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        private readonly List<WaitingListEntry> _waitingList = new();
        public IReadOnlyCollection<WaitingListEntry> WaitingList => _waitingList.AsReadOnly();

        public int Capacity => Location.Capacity;

        public Lesson(Guid workoutId, DateTime startAt, int durationMinutes, Location location, Guid? instructorId = null)
        {
            if (durationMinutes <= 0) throw new DomainException("Duration must be positive.");
            if (startAt == DateTime.MinValue) throw new DomainException("Start time is required.");

            WorkoutId = workoutId;
            StartAt = startAt;
            DurationMinutes = durationMinutes;
            Location = location ?? throw new ArgumentNullException(nameof(location));
            InstructorId = instructorId;
        }

        /// <summary>
        /// Moves the lesson to a new time/duration/location.
        /// </summary>
        public void Reschedule(DateTime startAt, int durationMinutes, Location location)
        {
            if (durationMinutes <= 0) throw new DomainException("Duration must be positive.");
            if (startAt == DateTime.MinValue) throw new DomainException("Start time is required.");

            StartAt = startAt;
            DurationMinutes = durationMinutes;
            Location = location ?? throw new ArgumentNullException(nameof(location));
        }

        /// <summary>
        /// Assigns (or clears) the instructor. A null value will mean "nnb" (nog niet bekend),
        /// also used when an instructor is replaced due to absence.
        /// </summary>
        public void ChangeInstructor(Guid? instructorId)
        {
            InstructorId = instructorId;
        }

        public Reservation? Reserve(Guid memberId, int? seatNumber = null)
        {
            if (_reservations.Any(r => r.MemberId == memberId && r.Status == ReservationStatus.Reserved))
                throw new DomainException("Member already has a reservation for this lesson.");

            if (_reservations.Count(r => r.Status == ReservationStatus.Reserved) >= Capacity)
            {
                var existing = _waitingList.FirstOrDefault(w => w.MemberId == memberId);
                if (existing != null) throw new DomainException("Member already on waiting list.");
                var entry = new WaitingListEntry(Id, memberId);
                _waitingList.Add(entry);
                return null;
            }

            if (seatNumber != null)
            {
                if (!Location.AllowsSeatSelection)
                    throw new DomainException("This location does not allow seat selection.");

                if (seatNumber < 1 || seatNumber > Capacity)
                    throw new DomainException($"Seat number must be between 1 and {Capacity}.");

                // Ensure seat is not already taken. A seat counts as taken for any non-cancelled
                // reservation (Reserved or Attended): an attended reservation still physically holds
                // the seat, and its row still occupies the unique (LessonId, SeatNumber) slot. This
                // matches how the API reports occupiedSeats to the client.
                if (_reservations.Any(r => r.Status != ReservationStatus.Cancelled && r.SeatNumber == seatNumber))
                    throw new DomainException("Seat already taken.");
            }

            var reservation = new Reservation(Id, memberId, seatNumber);
            _reservations.Add(reservation);
            return reservation;
        }

        /// <summary>
        /// Marks the member's active reservation for this lesson as attended (check-in at the door).
        /// </summary>
        public Reservation CheckIn(Guid memberId)
        {
            var res = _reservations.FirstOrDefault(r => r.MemberId == memberId && r.Status == ReservationStatus.Reserved);
            if (res == null) throw new DomainException("No active reservation to check in for this lesson.");
            res.MarkAttended();
            return res;
        }

        public void CancelReservation(Guid reservationId)
        {
            var res = _reservations.FirstOrDefault(r => r.Id == reservationId);
            if (res == null) throw new DomainException("Reservation not found.");
            res.Cancel();

            // No automatic promotion: when a spot frees up, all waiting members are notified and may
            // claim it on a first-come basis (handled in the application/UI layer).
        }

        /// <summary>
        /// Clears collected domain events. Should be called by the application layer
        /// after events have been dispatched.
        /// </summary>
        public void ClearDomainEvents()
        {
            _domainEvents.Clear();
        }
    }
}
