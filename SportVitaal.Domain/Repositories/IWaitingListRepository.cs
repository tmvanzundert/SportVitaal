using SportVitaal.Domain.Entities;

namespace SportVitaal.Domain.Repositories
{
    public interface IWaitingListRepository
    {
        Task<IEnumerable<WaitingListEntry>> GetForLessonAsync(Guid lessonId, CancellationToken ct = default);
        Task AddAsync(WaitingListEntry entry, CancellationToken ct = default);
        Task RemoveAsync(WaitingListEntry entry, CancellationToken ct = default);
    }
}

