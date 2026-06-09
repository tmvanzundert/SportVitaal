using System.Text.RegularExpressions;

namespace SportVitaal.Application.Services
{
    public static class PasswordPolicy
    {
        // Simple password strength check: min 8 chars, upper, lower, digit, special
        private static readonly Regex Upper = new Regex("[A-Z]", RegexOptions.Compiled);
        private static readonly Regex Lower = new Regex("[a-z]", RegexOptions.Compiled);
        private static readonly Regex Digit = new Regex("[0-9]", RegexOptions.Compiled);
        private static readonly Regex Special = new Regex("[^a-zA-Z0-9]", RegexOptions.Compiled);

        public static (bool IsValid, string[] Reasons) Validate(string password)
        {
            var reasons = new List<string>();
            if (string.IsNullOrEmpty(password) || password.Length < 8)
                reasons.Add("Password must be at least 8 characters long.");
            if (!Upper.IsMatch(password)) reasons.Add("Password must contain at least one uppercase letter.");
            if (!Lower.IsMatch(password)) reasons.Add("Password must contain at least one lowercase letter.");
            if (!Digit.IsMatch(password)) reasons.Add("Password must contain at least one digit.");
            if (!Special.IsMatch(password)) reasons.Add("Password must contain at least one special character (e.g. !@#$%).");

            return (reasons.Count == 0, reasons.ToArray());
        }
    }
}

