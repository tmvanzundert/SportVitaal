using SportVitaal.Domain.DomainEvents;
using SportVitaal.Domain.Services;
using SportVitaal.Domain.Repositories;
using SportVitaal.Domain.ValueObjects;


namespace SportVitaal.Application.Services
{
    /// <summary>
    /// Example/simple implementation of IDomainEventDispatcher that writes
    /// events to the console. Replace with a proper implementation that
    /// integrates with messaging/email services in production.
    /// </summary>
    public class DomainEventDispatcher : IDomainEventDispatcher
    {
        private readonly INotificationService _notificationService;
        private readonly IUserRepository _userRepository;

        public DomainEventDispatcher(INotificationService notificationService, IUserRepository userRepository)
        {
            _notificationService = notificationService;
            _userRepository = userRepository;
        }

        public async Task DispatchAsync(IDomainEvent domainEvent)
        {
            Console.WriteLine($"Domain event dispatched: {domainEvent.GetType().Name}");

            // Handle reservation promoted event by notifying the promoted member
            if (domainEvent is ReservationPromotedEvent promoted)
            {
                var user = await _userRepository.GetByIdAsync(promoted.MemberId);
                if (user != null && !string.IsNullOrWhiteSpace(user.Email))
                {
                    var to = new Email(user.Email);
                    var subject = "A spot became available for your lesson";
                    var body = $"Good news! A spot was freed up for the lesson {promoted.LessonId} and has been reserved for you. Reservation id: {promoted.ReservationId}.";
                    await _notificationService.SendEmailAsync(to, subject, body);
                }
            }

            // Handle reservation created event: confirmation to member
            if (domainEvent is ReservationCreatedEvent created)
            {
                var user = await _userRepository.GetByIdAsync(created.UserId);
                if (user != null && !string.IsNullOrWhiteSpace(user.Email))
                {
                    var to = new Email(user.Email);
                    var subject = "Your reservation is confirmed";
                    var body = $"Your reservation (id: {created.ReservationId}) for lesson {created.LessonId} is confirmed.";
                    await _notificationService.SendEmailAsync(to, subject, body);
                }
            }

            // Handle membership purchased event: send confirmation
            if (domainEvent is MembershipPurchasedEvent purchased)
            {
                var user = await _userRepository.GetByIdAsync(purchased.UserId);
                if (user != null && !string.IsNullOrWhiteSpace(user.Email))
                {
                    var to = new Email(user.Email);
                    var subject = "Membership purchase confirmation";
                    var body = $"Thank you for your purchase. Your membership was processed on {purchased.PurchasedAt:u}.";
                    await _notificationService.SendEmailAsync(to, subject, body);
                }
            }
        }
    }
}

