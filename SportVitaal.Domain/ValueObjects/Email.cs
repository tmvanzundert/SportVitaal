using System.Text.RegularExpressions;

namespace SportVitaal.Domain.ValueObjects
{
    public sealed class Email : ValueObject
    {
        private static readonly Regex EmailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        public string Address { get; }

        public Email(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) throw new ArgumentException("Email address must be provided.", nameof(address));
            address = address.Trim();
            if (!EmailRegex.IsMatch(address)) throw new ArgumentException("Invalid email address format.", nameof(address));
            Address = address;
        }

        public override string ToString() => Address;

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Address;
        }
    }
}

