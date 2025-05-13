namespace TeiasMongoAPI.Core.Models.Configuration
{
    public class RefreshTokenSettings
    {
        public int ExpirationDays { get; set; } = 7;
        public int RetentionDays { get; set; } = 30;
        public int MaxTokensPerUser { get; set; } = 10;
        public bool CleanupOnLogin { get; set; } = true;
    }
}