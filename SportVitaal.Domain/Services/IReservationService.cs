using SportVitaal.Domain.ValueObjects;

namespace SportVitaal.Domain.Services
{
    public interface IReservationService
    {
        /// <summary>
        /// Reserve a place for a user on a lesson. For spinning lessons a seat may be supplied.
        /// Throws domain exceptions when business rules are violated.
        /// </summary>
        Task ReserveAsync(Guid userId, Guid lessonId, Seat? seat = null, CancellationToken ct = default);

        /// <summary>
        /// Cancel an existing reservation. cancelledBy can be the user id or an employee id.
        /// </summary>
        Task CancelReservationAsync(Guid reservationId, Guid cancelledBy, CancellationToken ct = default);

        /// <summary>
        /// Promote next waiting-list entry for the provided lesson (if any) and create reservation.
        /// </summary>
        Task PromoteFromWaitingListAsync(Guid lessonId, CancellationToken ct = default);
    }
}

