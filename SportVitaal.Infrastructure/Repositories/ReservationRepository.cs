using Microsoft.EntityFrameworkCore;
using SportVitaal.Domain.Entities;
using SportVitaal.Domain.Repositories;
using SportVitaal.Infrastructure.Data;

namespace SportVitaal.Infrastructure.Repositories
{
    public class ReservationRepository : IReservationRepository
    {
        private readonly AppDbContext _db;
        public ReservationRepository(AppDbContext db) { _db = db; }

        public async Task AddAsync(Reservation reservation, CancellationToken ct = default)
        {
            await _db.Reservations.AddAsync(reservation, ct);
        }

        public async Task<IEnumerable<Reservation>> GetForLessonAsync(Guid lessonId, CancellationToken ct = default)
        {
            return await _db.Reservations.Where(r => r.LessonId == lessonId).ToListAsync(ct);
        }

        public async Task<IEnumerable<Reservation>> GetForUserAsync(Guid userId, CancellationToken ct = default)
        {
            return await _db.Reservations.Where(r => r.MemberId == userId).ToListAsync(ct);
        }

        public async Task<int> CountReservationsForUserInRangeAsync(Guid userId, DateTime from, DateTime to, CancellationToken ct = default)
        {
            // Join reservations with lessons to count only reservations for lessons in the given date range
            return await _db.Reservations
                .Where(r => r.MemberId == userId && r.Status == ReservationStatus.Reserved)
                .Join(_db.Lessons,
                    r => r.LessonId,
                    l => l.Id,
                    (r, l) => l)
                .Where(l => l.StartAt >= from && l.StartAt <= to)
                .CountAsync(ct);
        }

        public async Task<Reservation?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            return await _db.Reservations.FindAsync(new object[] { id }, ct);
        }

        public Task RemoveAsync(Reservation reservation, CancellationToken ct = default)
        {
            _db.Reservations.Remove(reservation);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Reservation reservation, CancellationToken ct = default)
        {
            _db.Reservations.Update(reservation);
            return Task.CompletedTask;
        }
    }
}

