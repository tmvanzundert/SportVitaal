using SportVitaal.Domain.Entities;

namespace SportVitaal.Domain.Repositories
{
    /// <summary>
    /// Memberships are modelled as an owned/value-like object on UserAccount.
    /// This repository provides operations to read/update a user's membership.
    /// </summary>
    public interface IMembershipRepository
    {
        Task<Membership?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
        Task AddOrUpdateAsync(Guid userId, Membership membership, CancellationToken ct = default);
    }
}
