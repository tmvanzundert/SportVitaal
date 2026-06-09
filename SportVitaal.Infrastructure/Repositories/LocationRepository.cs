using Microsoft.EntityFrameworkCore;
using SportVitaal.Domain.Entities;
using SportVitaal.Domain.Repositories;
using SportVitaal.Infrastructure.Data;

namespace SportVitaal.Infrastructure.Repositories
{
    public class LocationRepository : ILocationRepository
    {
        private readonly AppDbContext _db;
        public LocationRepository(AppDbContext db) { _db = db; }

        public async Task AddAsync(Location location, CancellationToken ct = default)
        {
            await _db.Locations.AddAsync(location, ct);
        }

        public async Task<IEnumerable<Location>> GetAllAsync(CancellationToken ct = default)
        {
            return await _db.Locations.ToListAsync(ct);
        }

        public async Task<Location> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            var loc = await _db.Locations.FindAsync(new object[] { id }, ct);
            if (loc == null) throw new KeyNotFoundException("Location not found.");
            return loc;
        }

        public Task RemoveAsync(Location location, CancellationToken ct = default)
        {
            _db.Locations.Remove(location);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Location location, CancellationToken ct = default)
        {
            _db.Locations.Update(location);
            return Task.CompletedTask;
        }
    }
}

