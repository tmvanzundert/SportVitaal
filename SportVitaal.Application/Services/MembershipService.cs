using SportVitaal.Domain.DomainEvents;
using SportVitaal.Domain.Entities;
using SportVitaal.Domain.Enums;
using SportVitaal.Domain.Repositories;
using SportVitaal.Domain.Services;
using SportVitaal.Domain.ValueObjects;
using SportVitaal.Domain.DomainExceptions;

namespace SportVitaal.Application.Services
{
    public class MembershipService : IMembershipService
    {
        private readonly IUserRepository _userRepository;
        private readonly IMembershipRepository _membershipRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IDomainEventDispatcher _dispatcher;

        public MembershipService(IUserRepository userRepository, IMembershipRepository membershipRepository, IUnitOfWork unitOfWork, IDomainEventDispatcher dispatcher)
        {
            _userRepository = userRepository;
            _membershipRepository = membershipRepository;
            _unitOfWork = unitOfWork;
            _dispatcher = dispatcher;
        }

        public async Task PurchaseMembershipAsync(Guid userId, MembershipType type, DateTime startDate, Money price, CancellationToken ct = default)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) throw new DomainException("User not found.");

            DateTime? end = type switch
            {
                MembershipType.TwiceWeeklyMonthly or MembershipType.UnlimitedMonthly => startDate.AddMonths(1),
                MembershipType.TwiceWeeklyYearly or MembershipType.UnlimitedYearly => startDate.AddYears(1),
                _ => throw new DomainException("Invalid membership type")
            };

            var membership = new Membership(type, startDate, end);
            user.StartMembership(membership);

            await _userRepository.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync(ct);

            var ev = new MembershipPurchasedEvent(Guid.Empty, user.Id);
            await _dispatcher.DispatchAsync(ev);
        }

        public async Task UpgradeMembershipAsync(Guid userId, MembershipType newType, Money priceDifference, CancellationToken ct = default)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) throw new DomainException("User not found.");

            var current = user.Membership ?? throw new DomainException("User has no active membership to upgrade.");

            // An upgrade keeps the already-paid-for period (the pro-rata difference is charged
            // separately) and only changes the tier.
            user.StartMembership(new Membership(newType, current.StartDate, current.EndDate));

            await _userRepository.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync(ct);

            var ev = new MembershipPurchasedEvent(Guid.Empty, user.Id);
            await _dispatcher.DispatchAsync(ev);
        }

        public async Task RenewMembershipAsync(Guid userId, CancellationToken ct = default)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) throw new DomainException("User not found.");

            var current = user.Membership ?? throw new DomainException("User has no membership to renew.");

            // Renewal extends coverage by one more period from the current end date, so members can
            // buy ahead before an expiring (yearly) subscription lapses.
            var baseDate = current.EndDate ?? DateTime.UtcNow;
            var newEnd = current.Type switch
            {
                MembershipType.TwiceWeeklyMonthly or MembershipType.UnlimitedMonthly => baseDate.AddMonths(1),
                MembershipType.TwiceWeeklyYearly or MembershipType.UnlimitedYearly => baseDate.AddYears(1),
                _ => throw new DomainException("Invalid membership type")
            };
            current.Extend(newEnd);

            await _userRepository.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync(ct);

            var ev = new MembershipPurchasedEvent(Guid.Empty, user.Id);
            await _dispatcher.DispatchAsync(ev);
        }

        public async Task CancelMembershipAsync(Guid userId, Guid cancelledBy, CancellationToken ct = default)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) throw new DomainException("User not found.");

            if (user.Membership == null) throw new DomainException("User has no active membership.");

            user.CancelMembership();

            await _userRepository.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync(ct);

            // Notify that the membership has (effectively) been ended
            var expiry = user.Membership?.EndDate ?? DateTime.UtcNow;
            var ev = new MembershipExpiringSoonEvent(Guid.Empty, user.Id, expiry);
            await _dispatcher.DispatchAsync(ev);
        }
    }
}


