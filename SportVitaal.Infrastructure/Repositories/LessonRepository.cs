using Microsoft.EntityFrameworkCore;
using SportVitaal.Domain.Entities;
using SportVitaal.Domain.Repositories;
using SportVitaal.Infrastructure.Data;

namespace SportVitaal.Infrastructure.Repositories
{
    public class LessonRepository : ILessonRepository
    {
        private readonly AppDbContext _db;

        public LessonRepository(AppDbContext db)
        {
            _db = db;
        }

        public async Task AddAsync(Lesson lesson)
        {
            await _db.Lessons.AddAsync(lesson);
        }

        public async Task DeleteAsync(Guid id)
        {
            var entity = await _db.Lessons.FindAsync(id);
            if (entity != null) _db.Lessons.Remove(entity);
        }

        public async Task<Lesson?> GetByIdAsync(Guid id)
        {
            return await _db.Lessons
                .Include(l => l.Reservations)
                .FirstOrDefaultAsync(l => l.Id == id);
        }

        public async Task<IEnumerable<Lesson>> GetLessonsInRangeAsync(DateTime from, DateTime to)
        {
            return await _db.Lessons
                .Include(l => l.Reservations)
                .Include(l => l.Location)
                .Where(l => l.StartAt >= from && l.StartAt <= to)
                .ToListAsync();
        }

        public async Task<IEnumerable<Lesson>> GetByWorkoutIdAsync(Guid workoutId)
        {
            return await _db.Lessons
                .Where(l => l.WorkoutId == workoutId)
                .ToListAsync();
        }

        public async Task<IEnumerable<Lesson>> GetForOccupancyAsync(DateTime from, DateTime to)
        {
            return await _db.Lessons
                .Include(l => l.Reservations)
                .Include(l => l.Location)
                .Where(l => l.StartAt >= from && l.StartAt <= to)
                .OrderBy(l => l.StartAt)
                .ToListAsync();
        }

        public Task UpdateAsync(Lesson lesson)
        {
            _db.Lessons.Update(lesson);
            return Task.CompletedTask;
        }
    }
}

