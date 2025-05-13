using BCrypt.Net;
using TeiasMongoAPI.Services.DTOs.Request.Auth;

namespace TeiasMongoAPI.Services.Security
{
    public interface IPasswordHashingService
    {
        string HashPassword(string password);
        bool VerifyPassword(string password, string passwordHash);
        bool IsPasswordComplex(string password);
    }

    public class PasswordHashingService : IPasswordHashingService
    {
        private const int WorkFactor = 12;

        public string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
        }

        public bool VerifyPassword(string password, string passwordHash)
        {
            try
            {
                return BCrypt.Net.BCrypt.Verify(password, passwordHash);
            }
            catch
            {
                return false;
            }
        }

        public bool IsPasswordComplex(string password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < PasswordRequirements.MinimumLength)
                return false;

            bool hasUpper = false;
            bool hasLower = false;
            bool hasNumber = false;
            bool hasSpecial = false;

            foreach (char c in password)
            {
                if (char.IsUpper(c)) hasUpper = true;
                else if (char.IsLower(c)) hasLower = true;
                else if (char.IsDigit(c)) hasNumber = true;
                else if (!char.IsLetterOrDigit(c)) hasSpecial = true;
            }

            return hasUpper && hasLower && hasNumber && hasSpecial;
        }
    }
}