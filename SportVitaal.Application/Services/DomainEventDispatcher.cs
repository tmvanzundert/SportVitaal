using SportVitaal.Domain.DomainEvents;
using SportVitaal.Domain.Entities;
using SportVitaal.Domain.Enums;
using SportVitaal.Domain.Services;
using SportVitaal.Domain.Repositories;
using SportVitaal.Domain.ValueObjects;

namespace SportVitaal.Application.Services
{
    /// <summary>
    /// Dispatches domain events to the notification side. Each handled event both persists an
    /// in-app <see cref="Notification"/> (surfaced in the MAUI app's feed) and sends an e-mail via
    /// <see cref="INotificationService"/>.
    /// </summary>
    public class DomainEventDispatcher : IDomainEventDispatcher
    {
        private readonly INotificationService _notificationService;
        private readonly IUserRepository _userRepository;
        private readonly IWaitingListRepository _waitingListRepository;
        private readonly INotificationRepository _notificationRepository;
        private readonly IUnitOfWork _unitOfWork;

        public DomainEventDispatcher(
            INotificationService notificationService,
            IUserRepository userRepository,
            IWaitingListRepository waitingListRepository,
            INotificationRepository notificationRepository,
            IUnitOfWork unitOfWork)
        {
            _notificationService = notificationService;
            _userRepository = userRepository;
            _waitingListRepository = waitingListRepository;
            _notificationRepository = notificationRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task DispatchAsync(IDomainEvent domainEvent)
        {
            Console.WriteLine($"Domain event dispatched: {domainEvent.GetType().Name}");

            switch (domainEvent)
            {
                // Reservation confirmation for the member.
                case ReservationCreatedEvent created:
                    await NotifyAsync(created.UserId, NotificationType.ReservationConfirmed,
                        "Je reservering is bevestigd",
                        "Je bent aangemeld voor de les. Je vindt de details in het lesrooster.");
                    break;

                // A spot freed up: notify everyone on the waiting list for this lesson so they can
                // race to claim the open spot (first come, first served) via the app.
                case ReservationCancelledEvent cancelled:
                {
                    var waiting = await _waitingListRepository.GetForLessonAsync(cancelled.LessonId);
                    foreach (var entry in waiting)
                    {
                        await NotifyAsync(entry.MemberId, NotificationType.WaitlistSpotAvailable,
                            "Er is een plek vrijgekomen voor je les",
                            "Goed nieuws! Er is een plek vrijgekomen voor een les waarvoor je op de "
                            + "wachtlijst staat. Reserveer snel in de app, want wie het eerst komt, "
                            + "het eerst maalt.");
                    }
                    break;
                }

                // Yearly membership expires in ~6 weeks: prompt the member to renew.
                case MembershipExpiringSoonEvent expiring:
                    await NotifyAsync(expiring.UserId, NotificationType.MembershipExpiring,
                        "Je abonnement verloopt binnenkort",
                        $"Je abonnement verloopt op {expiring.ExpiryDate:dd-MM-yyyy}. "
                        + "Koop alvast een nieuw abonnement zodat je zonder onderbreking kunt blijven sporten.");
                    break;

                // Membership purchase/renewal/upgrade confirmation.
                case MembershipPurchasedEvent purchased:
                    await NotifyAsync(purchased.UserId, NotificationType.MembershipPurchased,
                        "Bedankt voor je aankoop",
                        "Je abonnement is verwerkt. Je kunt het beheren onder 'Abonnement' in de app.");
                    break;
            }
        }

        /// <summary>
        /// Persists an in-app notification for the user and, when an e-mail address is known, also
        /// sends the same message by e-mail.
        /// </summary>
        private async Task NotifyAsync(Guid userId, NotificationType type, string title, string body)
        {
            await _notificationRepository.AddAsync(new Notification(userId, type, title, body));
            await _unitOfWork.SaveChangesAsync();

            var user = await _userRepository.GetByIdAsync(userId);
            if (user != null && !string.IsNullOrWhiteSpace(user.Email))
                await _notificationService.SendEmailAsync(new Email(user.Email), title, body);
        }
    }
}
