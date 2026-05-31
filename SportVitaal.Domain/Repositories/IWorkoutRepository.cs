using SportVitaal.Domain.Entities;

namespace SportVitaal.Domain.Repositories
{
    public interface IWorkoutRepository
    {
        Task<Workout?> GetByIdAsync(Guid id);
        Task<IEnumerable<Workout>> GetAllAsync();
        Task AddAsync(Workout workout);
        Task UpdateAsync(Workout workout);
        Task DeleteAsync(Guid id);
    }
}

