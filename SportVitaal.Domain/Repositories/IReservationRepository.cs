using SportVitaal.Domain.Entities;

namespace SportVitaal.Domain.Repositories
{
    public interface IReservationRepository
    {
        Task<Reservation?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<IEnumerable<Reservation>> GetForUserAsync(Guid userId, CancellationToken ct = default);
        Task<IEnumerable<Reservation>> GetForLessonAsync(Guid lessonId, CancellationToken ct = default);
        /// <summary>
        /// Count reservations for a user that have lessons between the given date range (inclusive).
        /// Only counts reservations with status Reserved.
        /// </summary>
        Task<int> CountReservationsForUserInRangeAsync(Guid userId, DateTime from, DateTime to, CancellationToken ct = default);
        Task AddAsync(Reservation reservation, CancellationToken ct = default);
        Task UpdateAsync(Reservation reservation, CancellationToken ct = default);
        Task RemoveAsync(Reservation reservation, CancellationToken ct = default);
    }
}

