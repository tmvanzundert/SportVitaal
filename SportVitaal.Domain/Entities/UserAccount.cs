using SportVitaal.Domain.Enums;
using SportVitaal.Domain.DomainExceptions;

namespace SportVitaal.Domain.Entities
{
    public class UserAccount : BaseEntity
    {
        // This fixes the database not being able to process the attributes via Pomelo.
        // We want to keep the properties private set to enforce invariants,
        // but EF needs a parameterless constructor.
        protected UserAccount() { }

        public string Email { get; private set; }
        public string? UserName { get; private set; }
        public string? FullName { get; private set; }
        public string? PhotoUrl { get; private set; }
        public Role Role { get; private set; }
        public bool IsActive { get; private set; }
        public Membership? Membership { get; private set; }

        public UserAccount(string email, Role role = Role.Member)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new DomainException("Email is required for user accounts.");

            Email = email.Trim().ToLowerInvariant();
            Role = role;
            IsActive = true;
        }

        public void UpdateProfile(string? userName, string? fullName, string? photoUrl)
        {
            if (!string.IsNullOrWhiteSpace(userName))
                UserName = userName!.Trim();

            FullName = string.IsNullOrWhiteSpace(fullName) ? FullName : fullName!.Trim();
            PhotoUrl = photoUrl ?? PhotoUrl;
        }

        public void StartMembership(Membership membership)
        {
            if (membership == null) throw new ArgumentNullException(nameof(membership));
            Membership = membership;
        }

        public void CancelMembership()
        {
            if (Membership == null) throw new DomainException("User has no active membership to cancel.");
            Membership.CancelIfMonthly();
        }
    }
}




