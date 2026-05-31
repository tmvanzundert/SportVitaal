namespace SportVitaal.Domain.Entities
{
    /// <summary>
    /// BaseEntity provides a common identity and creation timestamp for domain entities.
    /// 
    /// Usage:
    /// - Inherit from this class for domain entities and aggregate roots so they share a
    ///   consistent primary key type (<see cref="Guid"/>) and a creation timestamp.
    /// - Setters are protected to allow ORM materialization while preventing arbitrary
    ///   mutation from application code.
    /// </summary>
    public abstract class BaseEntity
    {
        public Guid Id { get; protected set; }
        public DateTime CreatedAt { get; protected set; }

        protected BaseEntity()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
        }
    }
}


