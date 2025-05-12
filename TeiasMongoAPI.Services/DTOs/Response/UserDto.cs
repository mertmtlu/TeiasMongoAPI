namespace TeiasMongoAPI.Services.DTOs.Response
{
    public class UserDto
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new();
        public List<string> Permissions { get; set; } = new();
        public bool IsActive { get; set; }
        public bool IsEmailVerified { get; set; }
        public DateTime? LastLoginDate { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }

        // Navigation properties
        public List<string> AssignedRegionIds { get; set; } = new();
        public List<string> AssignedTMIds { get; set; } = new();

        // Related entities (optional, for detailed views)
        public List<RegionDto>? AssignedRegions { get; set; }
        public List<TMDto>? AssignedTMs { get; set; }
    }

    // Simplified user DTO for lists
    public class UserListDto
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new();
        public bool IsActive { get; set; }
        public DateTime? LastLoginDate { get; set; }
    }

    // DTO for authentication responses
    public class AuthResponseDto
    {
        public UserDto User { get; set; } = new();
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }

    // DTO for refresh token response
    public class RefreshTokenDto
    {
        public string Token { get; set; } = string.Empty;
        public DateTime Expires { get; set; }
        public DateTime Created { get; set; }
        public bool IsActive { get; set; }
        public bool IsExpired { get; set; }
    }
}