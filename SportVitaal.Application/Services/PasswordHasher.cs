using System.Security.Cryptography;

namespace SportVitaal.Application.Services
{
    public static class PasswordHasher
    {
        // PBKDF2 parameters
        private const int Iterations = 100_000;
        private const int SaltSize = 16;
        private const int HashSize = 32;

        public static string HashPassword(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var derived = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
            var outBytes = new byte[1 + 4 + SaltSize + HashSize];
            int offset = 0;
            outBytes[offset++] = 0; // version
            var iterBytes = BitConverter.GetBytes(Iterations);
            Buffer.BlockCopy(iterBytes, 0, outBytes, offset, 4);
            offset += 4;
            Buffer.BlockCopy(salt, 0, outBytes, offset, SaltSize);
            offset += SaltSize;
            Buffer.BlockCopy(derived, 0, outBytes, offset, HashSize);
            return Convert.ToBase64String(outBytes);
        }

        public static bool Verify(string hashed, string provided)
        {
            try
            {
                var bytes = Convert.FromBase64String(hashed);
                int offset = 0;
                var version = bytes[offset++];
                var iterations = BitConverter.ToInt32(bytes, offset);
                offset += 4;
                var salt = new byte[SaltSize];
                Buffer.BlockCopy(bytes, offset, salt, 0, SaltSize);
                offset += SaltSize;
                var storedHash = new byte[HashSize];
                Buffer.BlockCopy(bytes, offset, storedHash, 0, HashSize);

                var derived = Rfc2898DeriveBytes.Pbkdf2(provided, salt, iterations, HashAlgorithmName.SHA256, HashSize);
                return CryptographicOperations.FixedTimeEquals(storedHash, derived);
            }
            catch
            {
                return false;
            }
        }
    }
}


