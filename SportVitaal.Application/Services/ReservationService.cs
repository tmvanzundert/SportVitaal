using SportVitaal.Domain.DomainEvents;
using SportVitaal.Domain.Entities;
using SportVitaal.Domain.Repositories;
using SportVitaal.Domain.Services;
using SportVitaal.Domain.DomainExceptions;
using SportVitaal.Domain.Enums;

namespace SportVitaal.Application.Services
{
    public class ReservationService : IReservationService
    {
        private readonly ILessonRepository _lessonRepository;
        private readonly IReservationRepository _reservationRepository;
        private readonly IWaitingListRepository _waitingListRepository;
        private readonly IUserRepository _userRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IDomainEventDispatcher _dispatcher;

        public ReservationService(
            ILessonRepository lessonRepository,
            IReservationRepository reservationRepository,
            IWaitingListRepository waitingListRepository,
            IUserRepository userRepository,
            IUnitOfWork unitOfWork,
            IDomainEventDispatcher dispatcher)
        {
            _lessonRepository = lessonRepository;
            _reservationRepository = reservationRepository;
            _waitingListRepository = waitingListRepository;
            _userRepository = userRepository;
            _unitOfWork = unitOfWork;
            _dispatcher = dispatcher;
        }

        public async Task ReserveAsync(Guid userId, Guid lessonId, int? seatNumber = null, CancellationToken ct = default)
        {
            var lesson = await _lessonRepository.GetByIdAsync(lessonId);
            if (lesson == null) throw new DomainException("Lesson not found.");

            // Reservation window: only allow reserving up to 1 week ahead
            if (lesson.StartAt > DateTime.UtcNow.AddDays(7))
                throw new DomainException("Reservations can only be made up to one week in advance.");

            // Load user and ensure active membership
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) throw new DomainException("User not found.");
            if (user.Membership == null || !user.Membership.IsActive)
                throw new DomainException("User must have an active membership to reserve lessons.");

            // Enforce weekly reservation limits based on membership type
            var membershipType = user.Membership.Type;
            int? weeklyLimit = membershipType switch
            {
                Domain.Enums.MembershipType.TwiceWeeklyMonthly => 2,
                Domain.Enums.MembershipType.TwiceWeeklyYearly => 2,
                _ => null // null means unlimited
            };

            if (weeklyLimit != null)
            {
                // determine week range (Monday .. Sunday) for the lesson start
                var lessonDate = lesson.StartAt.Date;
                var weekStart = lessonDate;
                while (weekStart.DayOfWeek != DayOfWeek.Monday)
                    weekStart = weekStart.AddDays(-1);
                var weekEnd = weekStart.AddDays(7).AddTicks(-1);

                var count = await _reservationRepository.CountReservationsForUserInRangeAsync(userId, weekStart, weekEnd, ct);
                if (count >= weeklyLimit.Value)
                    throw new DomainException($"Membership allows a maximum of {weeklyLimit} reservations per week.");
            }

            // Seats are flat 1..Capacity numbers (e.g. the 24 spinning bikes). Range and
            // availability are validated by the Lesson aggregate in Reserve below.

            // Capture existing waiting-list entries so we can identify any new one created below.
            var waitingBefore = lesson.WaitingList.Select(w => w.Id).ToHashSet();

            var reservation = lesson.Reserve(userId, seatNumber);

            // New entities carry a client-generated Guid key, so EF would mark them Modified
            // (and issue an UPDATE against a non-existent row) if left to graph discovery.
            // Add them explicitly so they are tracked as Added and inserted.
            if (reservation != null)
            {
                await _reservationRepository.AddAsync(reservation, ct);
            }
            else
            {
                foreach (var entry in lesson.WaitingList.Where(w => !waitingBefore.Contains(w.Id)))
                    await _waitingListRepository.AddAsync(entry, ct);
            }

            await _unitOfWork.SaveChangesAsync(ct);

            if (reservation == null)
            {
                // Member was added to the waiting list; they are notified when a spot frees up
                // (see ReservationCancelledEvent handling), so nothing to dispatch on join.
                return;
            }

            // If the member was on the waiting list for this lesson, they now hold a spot — clear it.
            var myWaiting = (await _waitingListRepository.GetForLessonAsync(lessonId, ct))
                .Where(w => w.MemberId == userId).ToList();
            if (myWaiting.Count > 0)
            {
                foreach (var entry in myWaiting) await _waitingListRepository.RemoveAsync(entry, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }

            var createdEvent = new ReservationCreatedEvent(reservation.Id, reservation.LessonId, reservation.MemberId);
            await _dispatcher.DispatchAsync(createdEvent);
        }

        public async Task LeaveWaitlistAsync(Guid userId, Guid lessonId, CancellationToken ct = default)
        {
            var entries = (await _waitingListRepository.GetForLessonAsync(lessonId, ct))
                .Where(w => w.MemberId == userId).ToList();
            if (entries.Count == 0) return;

            foreach (var entry in entries) await _waitingListRepository.RemoveAsync(entry, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        public async Task CheckInAsync(Guid userId, Guid lessonId, CancellationToken ct = default)
        {
            var lesson = await _lessonRepository.GetByIdAsync(lessonId);
            if (lesson == null) throw new DomainException("Lesson not found.");

            // Check-in opens 30 minutes before the lesson and closes when the lesson ends.
            var now = DateTime.UtcNow;
            if (now < lesson.StartAt.AddMinutes(-30))
                throw new DomainException("Check-in opens 30 minutes before the lesson starts.");
            if (now > lesson.StartAt.AddMinutes(lesson.DurationMinutes))
                throw new DomainException("This lesson has already ended.");

            lesson.CheckIn(userId);

            await _lessonRepository.UpdateAsync(lesson);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        public async Task CancelReservationAsync(Guid reservationId, Guid cancelledBy, CancellationToken ct = default)
        {
            var reservation = await _reservationRepository.GetByIdAsync(reservationId);
            if (reservation == null) throw new DomainException("Reservation not found.");

            var lesson = await _lessonRepository.GetByIdAsync(reservation.LessonId);
            if (lesson == null) throw new DomainException("Associated lesson not found.");

            // Determine who is cancelling and enforce cancellation window rules
            var canceller = await _userRepository.GetByIdAsync(cancelledBy);
            if (canceller == null) throw new DomainException("Cancelling user not found.");

            // If the member cancels themselves enforce the 1 hour rule. Staff (Employee/Instructor) may cancel anytime.
            if (cancelledBy == reservation.MemberId)
            {
                if (DateTime.UtcNow > lesson.StartAt.AddHours(-1))
                    throw new DomainException("Cannot cancel reservation within one hour of the lesson start.");
            }
            else
            {
                if (canceller.Role != Role.Employee && canceller.Role != Role.Instructor)
                    throw new DomainException("Only staff may cancel other members' reservations.");
            }

            lesson.CancelReservation(reservationId);

            await _lessonRepository.UpdateAsync(lesson);
            await _unitOfWork.SaveChangesAsync(ct);

            var cancelledEvent = new ReservationCancelledEvent(reservation.Id, reservation.LessonId, reservation.MemberId, cancelledBy);
            await _dispatcher.DispatchAsync(cancelledEvent);

            // Dispatch any domain events produced by the lesson (e.g. ReservationPromotedEvent)
            foreach (var ev in lesson.DomainEvents)
            {
                await _dispatcher.DispatchAsync(ev);
            }
            lesson.ClearDomainEvents();
        }

        public async Task PromoteFromWaitingListAsync(Guid lessonId, CancellationToken ct = default)
        {
            var lesson = await _lessonRepository.GetByIdAsync(lessonId);
            if (lesson == null) throw new DomainException("Lesson not found.");

            var waiting = await _waitingListRepository.GetForLessonAsync(lessonId, ct);
            var next = waiting.OrderBy(w => w.CreatedAt).FirstOrDefault();
            if (next == null) return; // nothing to promote

            // Check capacity
            var reservedCount = lesson.Reservations.Count(r => r.Status == ReservationStatus.Reserved);
            if (reservedCount >= lesson.Capacity) return;

            var reservation = lesson.Reserve(next.MemberId, null);
            if (reservation == null) return; // unexpected: could not create reservation

            // Add explicitly so the client-generated key is tracked as Added (insert), not Modified.
            await _reservationRepository.AddAsync(reservation, ct);

            // remove waiting list entry
            await _waitingListRepository.RemoveAsync(next, ct);

            await _unitOfWork.SaveChangesAsync(ct);

            var createdEvent = new ReservationCreatedEvent(reservation.Id, reservation.LessonId, reservation.MemberId);
            await _dispatcher.DispatchAsync(createdEvent);

            // dispatch any additional domain events
            foreach (var ev in lesson.DomainEvents)
            {
                await _dispatcher.DispatchAsync(ev);
            }
            lesson.ClearDomainEvents();
        }
    }
}

