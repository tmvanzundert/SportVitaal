using SportVitaal.Domain.Entities;

namespace SportVitaal.Domain.Repositories
{
    public interface ILessonRepository
    {
        Task<Lesson?> GetByIdAsync(Guid id);
        Task<IEnumerable<Lesson>> GetLessonsInRangeAsync(DateTime from, DateTime to);
        Task AddAsync(Lesson lesson);
        Task UpdateAsync(Lesson lesson);
        Task DeleteAsync(Guid id);
    }
}

