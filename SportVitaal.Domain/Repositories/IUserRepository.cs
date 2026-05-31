using SportVitaal.Domain.Entities;

namespace SportVitaal.Domain.Repositories
{
    public interface IUserRepository
    {
        Task<UserAccount?> GetByIdAsync(Guid id);
        Task<UserAccount?> GetByEmailAsync(string email);
        Task AddAsync(UserAccount user);
        Task UpdateAsync(UserAccount user);
    }
}

