using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportVitaal.Domain.Repositories;

namespace SportVitaal.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationRepository _notifications;
        private readonly IUnitOfWork _uow;

        public NotificationsController(INotificationRepository notifications, IUnitOfWork uow)
        {
            _notifications = notifications;
            _uow = uow;
        }

        // The caller's notification feed (newest first) plus the unread count for the badge.
        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken ct)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();

            var items = await _notifications.GetForUserAsync(userId, ct);
            return Ok(new
            {
                unreadCount = items.Count(n => !n.IsRead),
                items = items.Select(n => new
                {
                    n.Id,
                    type = n.Type.ToString(),
                    n.Title,
                    n.Body,
                    n.CreatedAt,
                    read = n.IsRead
                })
            });
        }

        // Lightweight badge count, so the shell can poll without pulling the whole feed.
        [HttpGet("unread-count")]
        public async Task<IActionResult> UnreadCount(CancellationToken ct)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            return Ok(new { unreadCount = await _notifications.CountUnreadAsync(userId, ct) });
        }

        [HttpPost("{id:guid}/read")]
        public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();

            var notification = await _notifications.GetByIdAsync(id, ct);
            if (notification == null || notification.UserId != userId) return NotFound();

            notification.MarkRead();
            await _uow.SaveChangesAsync(ct);
            return NoContent();
        }

        [HttpPost("read-all")]
        public async Task<IActionResult> MarkAllRead(CancellationToken ct)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();

            await _notifications.MarkAllReadAsync(userId, ct);
            await _uow.SaveChangesAsync(ct);
            return NoContent();
        }

        private bool TryGetUserId(out Guid userId)
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(idClaim, out userId);
        }
    }
}
