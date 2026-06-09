using Microsoft.EntityFrameworkCore;
using SportVitaal.Domain.Entities;
using SportVitaal.Domain.Repositories;
using SportVitaal.Infrastructure.Data;

namespace SportVitaal.Infrastructure.Repositories
{
    public class WorkoutRepository : IWorkoutRepository
    {
        private readonly AppDbContext _db;
        public WorkoutRepository(AppDbContext db) { _db = db; }

        public async Task AddAsync(Workout workout)
        {
            await _db.Workouts.AddAsync(workout);
        }

        public async Task DeleteAsync(Guid id)
        {
            var w = await _db.Workouts.FindAsync(id);
            if (w != null) _db.Workouts.Remove(w);
        }

        public async Task<IEnumerable<Workout>> GetAllAsync()
        {
            return await _db.Workouts.ToListAsync();
        }

        public async Task<Workout?> GetByIdAsync(Guid id)
        {
            return await _db.Workouts.FindAsync(id);
        }

        public Task UpdateAsync(Workout workout)
        {
            _db.Workouts.Update(workout);
            return Task.CompletedTask;
        }
    }
}

