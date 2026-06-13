using Microsoft.EntityFrameworkCore;
using SportVitaal.Domain.Entities;
using SportVitaal.Domain.Repositories;
using SportVitaal.Infrastructure.Data;

namespace SportVitaal.Infrastructure.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext _db;
        public UserRepository(AppDbContext db) { _db = db; }

        public async Task AddAsync(UserAccount user)
        {
            await _db.Users.AddAsync(user);
        }

        public async Task<UserAccount?> GetByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;
            var trimmed = email.Trim();
            return await _db.Users.FirstOrDefaultAsync(u => u.Email == trimmed);
        }

        public async Task<UserAccount?> GetByIdAsync(Guid id)
        {
            return await _db.Users.FindAsync(id);
        }

        public async Task<IEnumerable<UserAccount>> GetByRoleAsync(SportVitaal.Domain.Enums.Role role)
        {
            return await _db.Users.Where(u => u.Role == role).ToListAsync();
        }

        public Task UpdateAsync(UserAccount user)
        {
            _db.Users.Update(user);
            return Task.CompletedTask;
        }

        public async Task StartMembershipAsync(Guid userId, Membership membership, CancellationToken ct = default)
        {
            var user = await _db.Users.FindAsync(new object[] { userId }, ct);
            if (user == null) throw new InvalidOperationException("User not found.");
            user.StartMembership(membership);
            _db.Users.Update(user);
        }

        public async Task CancelMembershipAsync(Guid userId, CancellationToken ct = default)
        {
            var user = await _db.Users.FindAsync(new object[] { userId }, ct);
            if (user == null) throw new InvalidOperationException("User not found.");
            user.CancelMembership();
            _db.Users.Update(user);
        }
    }
}

