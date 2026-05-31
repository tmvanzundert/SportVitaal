using SportVitaal.Domain.DomainExceptions;
using SportVitaal.Domain.DomainEvents;

namespace SportVitaal.Domain.Entities
{
    public class Lesson : BaseEntity
    {
        // This fixes the database not being able to process the attributes via Pomelo.
        // We want to keep the properties private set to enforce invariants,
        // but EF needs a parameterless constructor.
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

        public Reservation? Reserve(Guid memberId, int? seatNumber = null)
        {
            if (_reservations.Any(r => r.MemberId == memberId && r.Status == ReservationStatus.Reserved))
                throw new DomainException("Member already has a reservation for this lesson.");

            if (_reservations.Count(r => r.Status == ReservationStatus.Reserved) >= Capacity)
            {
                // add to waiting list
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

                // Ensure seat is not already taken by an active reservation
                if (_reservations.Any(r => r.Status == ReservationStatus.Reserved && r.SeatNumber == seatNumber))
                    throw new DomainException("Seat already taken.");
            }

            var reservation = new Reservation(Id, memberId, seatNumber);
            _reservations.Add(reservation);
            return reservation;
        }

        public void CancelReservation(Guid reservationId)
        {
            var res = _reservations.FirstOrDefault(r => r.Id == reservationId);
            if (res == null) throw new DomainException("Reservation not found.");
            res.Cancel();

            // promote first on waiting list if any
            var next = _waitingList.FirstOrDefault();
            if (next != null)
            {
                _waitingList.Remove(next);
                var promoted = new Reservation(Id, next.MemberId, null);
                _reservations.Add(promoted);
                // Record a domain event so the application/infrastructure layers can notify the promoted member
                _domainEvents.Add(new ReservationPromotedEvent(promoted.LessonId, promoted.MemberId, promoted.Id, DateTime.UtcNow));
            }
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
