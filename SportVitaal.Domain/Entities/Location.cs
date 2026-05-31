using SportVitaal.Domain.DomainExceptions;

namespace SportVitaal.Domain.Entities
{
    public class Location : BaseEntity
    {
        // This fixes the database not being able to process the attributes via Pomelo.
        // We want to keep the properties private set to enforce invariants,
        // but EF needs a parameterless constructor.
        protected Location() { }
        public string Name { get; private set; }
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
    }
}
