using Microsoft.EntityFrameworkCore;
using SportVitaal.Domain.Entities;
using SportVitaal.Domain.Repositories;
using SportVitaal.Infrastructure.Data;

namespace SportVitaal.Infrastructure.Repositories
{
    public class NotificationRepository : INotificationRepository
    {
        private readonly AppDbContext _db;
        public NotificationRepository(AppDbContext db) { _db = db; }

        public async Task AddAsync(Notification notification, CancellationToken ct = default)
        {
            await _db.Notifications.AddAsync(notification, ct);
        }

        public async Task<IReadOnlyList<Notification>> GetForUserAsync(Guid userId, CancellationToken ct = default)
        {
            return await _db.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync(ct);
        }

        public async Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            return await _db.Notifications.FirstOrDefaultAsync(n => n.Id == id, ct);
        }

        public async Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default)
        {
            return await _db.Notifications.CountAsync(n => n.UserId == userId && n.ReadAt == null, ct);
        }

        public async Task<int> MarkAllReadAsync(Guid userId, CancellationToken ct = default)
        {
            var unread = await _db.Notifications
                .Where(n => n.UserId == userId && n.ReadAt == null)
                .ToListAsync(ct);

            foreach (var n in unread) n.MarkRead();
            return unread.Count;
        }
    }
}
