using SportVitaal.Domain.DomainExceptions;
using SportVitaal.Domain.Enums;

namespace SportVitaal.Domain.Entities
{
    /// <summary>
    /// A persisted in-app notification for a single user (member or instructor). Created by the
    /// domain-event pipeline (e.g. a freed waiting-list spot or an expiring membership) and read
    /// by the MAUI app's notification feed.
    /// </summary>
    public class Notification : BaseEntity
    {
        // EF needs a parameterless constructor; keep setters private to enforce invariants.
        protected Notification() { }

        public Guid UserId { get; private set; }
        public NotificationType Type { get; private set; }
        public string Title { get; private set; } = null!;
        public string Body { get; private set; } = null!;
        public DateTime? ReadAt { get; private set; }

        public bool IsRead => ReadAt != null;

        public Notification(Guid userId, NotificationType type, string title, string body)
        {
            if (userId == Guid.Empty) throw new DomainException("Notification requires a user.");
            if (string.IsNullOrWhiteSpace(title)) throw new DomainException("Notification title is required.");

            UserId = userId;
            Type = type;
            Title = title.Trim();
            Body = (body ?? string.Empty).Trim();
        }

        /// <summary>Marks the notification as read. Idempotent: re-reading keeps the original timestamp.</summary>
        public void MarkRead()
        {
            ReadAt ??= DateTime.UtcNow;
        }
    }
}
