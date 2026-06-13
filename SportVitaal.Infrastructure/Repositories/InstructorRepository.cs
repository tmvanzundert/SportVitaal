using Microsoft.EntityFrameworkCore;
using SportVitaal.Domain.Entities;
using SportVitaal.Domain.Repositories;
using SportVitaal.Infrastructure.Data;

namespace SportVitaal.Infrastructure.Repositories
{
    public class InstructorRepository : IInstructorRepository
    {
        private readonly AppDbContext _db;
        public InstructorRepository(AppDbContext db) { _db = db; }

        public async Task AddAsync(Instructor instructor)
        {
            await _db.Instructors.AddAsync(instructor);
        }

        public async Task DeleteAsync(Guid id)
        {
            var entity = await _db.Instructors.FindAsync(id);
            if (entity != null) _db.Instructors.Remove(entity);
        }

        public async Task<IEnumerable<Instructor>> GetAllAsync()
        {
            return await _db.Instructors.ToListAsync();
        }

        public async Task<Instructor?> GetByIdAsync(Guid id)
        {
            return await _db.Instructors.FindAsync(id);
        }

        public Task UpdateAsync(Instructor instructor)
        {
            _db.Instructors.Update(instructor);
            return Task.CompletedTask;
        }
    }
}
