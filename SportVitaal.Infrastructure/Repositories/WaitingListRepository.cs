using Microsoft.EntityFrameworkCore;
using SportVitaal.Domain.Entities;
using SportVitaal.Domain.Repositories;
using SportVitaal.Infrastructure.Data;

namespace SportVitaal.Infrastructure.Repositories
{
    public class WaitingListRepository : IWaitingListRepository
    {
        private readonly AppDbContext _db;
        public WaitingListRepository(AppDbContext db) { _db = db; }

        public async Task AddAsync(WaitingListEntry entry, CancellationToken ct = default)
        {
            await _db.Set<WaitingListEntry>().AddAsync(entry, ct);
        }

        public async Task<IEnumerable<WaitingListEntry>> GetForLessonAsync(Guid lessonId, CancellationToken ct = default)
        {
            return await _db.Set<WaitingListEntry>().Where(w => w.LessonId == lessonId).ToListAsync(ct);
        }

        public async Task<IEnumerable<WaitingListEntry>> GetForUserAsync(Guid userId, CancellationToken ct = default)
        {
            return await _db.Set<WaitingListEntry>().Where(w => w.MemberId == userId).ToListAsync(ct);
        }

        public Task RemoveAsync(WaitingListEntry entry, CancellationToken ct = default)
        {
            _db.Set<WaitingListEntry>().Remove(entry);
            return Task.CompletedTask;
        }
    }
}

