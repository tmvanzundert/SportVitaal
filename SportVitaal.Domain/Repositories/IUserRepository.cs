using SportVitaal.Domain.Entities;

namespace SportVitaal.Domain.Repositories
{
    public interface IUserRepository
    {
        Task<UserAccount?> GetByIdAsync(Guid id);
        Task<UserAccount?> GetByEmailAsync(string email);
        Task<IEnumerable<UserAccount>> GetByRoleAsync(Enums.Role role);
        Task AddAsync(UserAccount user);
        Task UpdateAsync(UserAccount user);
        /// <summary>
        /// Start a membership for the given user.
        /// </summary>
        Task StartMembershipAsync(Guid userId, Membership membership, CancellationToken ct = default);

        /// <summary>
        /// Cancel the active membership for the given user (if allowed by membership rules).
        /// </summary>
        Task CancelMembershipAsync(Guid userId, CancellationToken ct = default);
    }
}

