using SportVitaal.Domain.Entities;

namespace SportVitaal.Domain.Repositories
{
    public interface ILocationRepository
    {
        Task<Location> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<IEnumerable<Location>> GetAllAsync(CancellationToken ct = default);
        Task AddAsync(Location location, CancellationToken ct = default);
        Task UpdateAsync(Location location, CancellationToken ct = default);
        Task RemoveAsync(Location location, CancellationToken ct = default);
    }
}

