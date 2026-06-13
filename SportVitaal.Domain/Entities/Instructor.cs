using SportVitaal.Domain.DomainExceptions;

namespace SportVitaal.Domain.Entities
{
    public class Instructor : BaseEntity
    {
        // EF needs a parameterless constructor; keep setters private to enforce invariants.
        protected Instructor() { }

        public string Name { get; private set; } = null!;
        public string? PhotoUrl { get; private set; }

        public Instructor(string name, string? photoUrl = null)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Instructor name is required.");

            Name = name.Trim();
            PhotoUrl = photoUrl;
        }

        public void Rename(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Instructor name is required.");
            Name = name.Trim();
        }

        public void SetPhoto(string? photoUrl)
        {
            PhotoUrl = photoUrl;
        }
    }
}
