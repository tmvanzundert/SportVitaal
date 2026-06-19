using SportVitaal.Domain.DomainExceptions;

namespace SportVitaal.Domain.Entities
{
    public class Location : BaseEntity
    {
        // EF needs a parameterless constructor; keep setters private to enforce invariants.
        protected Location() { }
        public string Name { get; private set; } = null!;
        public int Capacity { get; private set; }
        public bool AllowsSeatSelection { get; private set; }

        public Location(string name, int capacity, bool allowsSeatSelection = false)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Location name is required.");
            if (capacity <= 0) throw new DomainException("Capacity must be positive.");

            Name = name.Trim();
            Capacity = capacity;
            AllowsSeatSelection = allowsSeatSelection;
        }

        public void Update(string name, int capacity, bool allowsSeatSelection)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Location name is required.");
            if (capacity <= 0) throw new DomainException("Capacity must be positive.");
            Name = name.Trim();
            Capacity = capacity;
            AllowsSeatSelection = allowsSeatSelection;
        }
    }
}
