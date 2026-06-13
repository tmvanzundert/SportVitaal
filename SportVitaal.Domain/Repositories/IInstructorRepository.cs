using SportVitaal.Domain.Entities;

namespace SportVitaal.Domain.Repositories
{
    public interface IInstructorRepository
    {
        Task<Instructor?> GetByIdAsync(Guid id);
        Task<IEnumerable<Instructor>> GetAllAsync();
        Task AddAsync(Instructor instructor);
        Task UpdateAsync(Instructor instructor);
        Task DeleteAsync(Guid id);
    }
}
