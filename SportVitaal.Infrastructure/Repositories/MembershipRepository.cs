using Microsoft.EntityFrameworkCore;
using SportVitaal.Domain.Entities;
using SportVitaal.Domain.Repositories;
using SportVitaal.Infrastructure.Data;

namespace SportVitaal.Infrastructure.Repositories
{
    public class MembershipRepository : IMembershipRepository
    {
        private readonly AppDbContext _db;
        public MembershipRepository(AppDbContext db) { _db = db; }

        public async Task AddOrUpdateAsync(Guid userId, Membership membership, CancellationToken ct = default)
        {
            var user = await _db.Users.FindAsync(new object[] { userId }, ct);
            if (user == null) throw new InvalidOperationException("User not found when adding/updating membership.");

            var entry = _db.Entry(user);
            // Set the owned Membership navigation value
            entry.Reference("Membership").CurrentValue = membership;
            _db.Users.Update(user);
        }

        public async Task<Membership?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        {
            return await _db.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.Membership)
                .FirstOrDefaultAsync(ct);
        }
    }
}
