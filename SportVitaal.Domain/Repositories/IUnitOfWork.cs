namespace SportVitaal.Domain.Repositories
{
    public interface IUnitOfWork
    {
        /// <summary>
        /// Persist changes made through repositories.
        /// </summary>
        Task<int> SaveChangesAsync(CancellationToken ct = default);
    }
}

