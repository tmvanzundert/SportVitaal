namespace SportVitaal.Domain.Services
{
    public interface IReservationService
    {
        /// <summary>
        /// Reserve a place for a user on a lesson. For spinning lessons a specific seat (bike) may be
        /// supplied as a flat seat number (1..Capacity). Throws domain exceptions when business rules
        /// are violated.
        /// </summary>
        Task ReserveAsync(Guid userId, Guid lessonId, int? seatNumber = null, CancellationToken ct = default);

        /// <summary>
        /// Cancel an existing reservation. cancelledBy can be the user id or an employee id.
        /// </summary>
        Task CancelReservationAsync(Guid reservationId, Guid cancelledBy, CancellationToken ct = default);

        /// <summary>
        /// Remove the user from a lesson's waiting list.
        /// </summary>
        Task LeaveWaitlistAsync(Guid userId, Guid lessonId, CancellationToken ct = default);

        /// <summary>
        /// Registers the member as attended for a lesson (check-in at the door). Only valid within
        /// the check-in window around the lesson start. Throws domain exceptions on rule violations.
        /// </summary>
        Task CheckInAsync(Guid userId, Guid lessonId, CancellationToken ct = default);

        /// <summary>
        /// Promote next waiting-list entry for the provided lesson (if any) and create reservation.
        /// </summary>
        Task PromoteFromWaitingListAsync(Guid lessonId, CancellationToken ct = default);
    }
}

