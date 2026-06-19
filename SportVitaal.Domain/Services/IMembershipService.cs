using SportVitaal.Domain.ValueObjects;
using SportVitaal.Domain.Enums;

namespace SportVitaal.Domain.Services
{
    public interface IMembershipService
    {
        /// <summary>
        /// Purchase a membership for a user that starts at startDate.
        /// </summary>
        Task PurchaseMembershipAsync(Guid userId, MembershipType type, DateTime startDate, Money price, CancellationToken ct = default);

        /// <summary>
        /// Upgrade an existing membership for a user to a new type.
        /// </summary>
        Task UpgradeMembershipAsync(Guid userId, MembershipType newType, Money priceDifference, CancellationToken ct = default);

        /// <summary>
        /// Renew (extend) an existing membership by one more period from its current end date.
        /// </summary>
        Task RenewMembershipAsync(Guid userId, CancellationToken ct = default);

        /// <summary>
        /// Cancel membership for the given user if allowed by type/rules.
        /// </summary>
        Task CancelMembershipAsync(Guid userId, Guid cancelledBy, CancellationToken ct = default);
    }
}

