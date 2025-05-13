namespace TeiasMongoAPI.Services.DTOs.Request.Auth
{
    public static class PasswordRequirements
    {
        public const int MinimumLength = 8;
        public const int MaximumLength = 100;
        public const bool RequireUppercase = true;
        public const bool RequireLowercase = true;
        public const bool RequireDigit = true;
        public const bool RequireSpecialCharacter = true;

        public static string GetPasswordPolicy()
        {
            return "Password must be at least 8 characters long and contain at least one uppercase letter, one lowercase letter, one digit, and one special character.";
        }
    }
}