using SportVitaal.Domain.DomainExceptions;

namespace SportVitaal.Domain.Entities
{
    public class Workout : BaseEntity
    {
        // EF needs a parameterless constructor; keep setters private to enforce invariants.
        protected Workout() { }
        public string Name { get; private set; } = null!;
        public string? Description { get; private set; }
        public int DefaultDurationMinutes { get; private set; }

        public Workout(string name, int defaultDurationMinutes, string? description = null)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Workout name is required.");
            if (defaultDurationMinutes <= 0) throw new DomainException("Duration must be positive.");

            Name = name.Trim();
            DefaultDurationMinutes = defaultDurationMinutes;
            Description = description;
        }

        public void Update(string name, int durationMinutes, string? description)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Workout name is required.");
            if (durationMinutes <= 0) throw new DomainException("Duration must be positive.");

            Name = name.Trim();
            DefaultDurationMinutes = durationMinutes;
            Description = description;
        }
    }
}
