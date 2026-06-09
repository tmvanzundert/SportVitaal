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

            // Simple upgrade policy: start a new membership period from now with the new type
            DateTime start = DateTime.UtcNow;
            DateTime? end = newType switch
            {
                MembershipType.TwiceWeeklyMonthly or MembershipType.UnlimitedMonthly => start.AddMonths(1),
                MembershipType.TwiceWeeklyYearly or MembershipType.UnlimitedYearly => start.AddYears(1),
                _ => throw new DomainException("Invalid membership type")
            };

            var membership = new Membership(newType, start, end);
            user.StartMembership(membership);

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


