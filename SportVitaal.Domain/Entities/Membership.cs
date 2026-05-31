using SportVitaal.Domain.Enums;
using SportVitaal.Domain.DomainExceptions;

namespace SportVitaal.Domain.Entities
{
    // A lightweight value-like entity capturing subscription state
    public class Membership
    {
        public MembershipType Type { get; private set; }
        public DateTime StartDate { get; private set; }
        public DateTime? EndDate { get; private set; }
        public bool IsActive => Type != MembershipType.None && (EndDate == null || EndDate > DateTime.UtcNow);

        public Membership(MembershipType type, DateTime startDate, DateTime? endDate)
        {
            if (type == MembershipType.None)
                throw new DomainException("Cannot create a membership with type 'None'.");

            if (endDate != null && endDate <= startDate)
                throw new DomainException("End date must be after start date.");

            Type = type;
            StartDate = startDate;
            EndDate = endDate;
        }

        public void Extend(DateTime newEnd)
        {
            if (newEnd <= StartDate)
                throw new DomainException("New end date must be after start date.");

            if (EndDate == null || newEnd > EndDate)
            {
                EndDate = newEnd;
            }
        }

        public void CancelIfMonthly()
        {
            // Business rule: monthly subscriptions can be cancelled; yearly cannot
            if (Type == MembershipType.TwiceWeeklyMonthly || Type == MembershipType.UnlimitedMonthly)
            {
                EndDate = DateTime.UtcNow; // effective immediately
                return;
            }

            throw new DomainException("Only monthly subscriptions can be cancelled programmatically.");
        }
    }
}

