using SportVitaal.Domain.Entities;

namespace SportVitaal.Domain.Repositories
{
    public interface INotificationRepository
    {
        Task AddAsync(Notification notification, CancellationToken ct = default);

        /// <summary>All notifications for a user, newest first.</summary>
        Task<IReadOnlyList<Notification>> GetForUserAsync(Guid userId, CancellationToken ct = default);

        Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default);

        /// <summary>Number of unread notifications for a user (for the badge count).</summary>
        Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default);

        /// <summary>Marks every unread notification for the user as read. Returns the number updated.</summary>
        Task<int> MarkAllReadAsync(Guid userId, CancellationToken ct = default);
    }
}
