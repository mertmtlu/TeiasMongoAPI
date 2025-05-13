using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.KeyModels;

namespace TeiasMongoAPI.Core.Interfaces.Repositories
{
    public interface IUserRepository : IGenericRepository<User>
    {
        Task<User> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
        Task<User> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
        Task<User> GetByEmailOrUsernameAsync(string emailOrUsername, CancellationToken cancellationToken = default);
        Task<User> GetByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
        Task<User> GetByPasswordResetTokenAsync(string resetToken, CancellationToken cancellationToken = default);
        Task<User> GetByEmailVerificationTokenAsync(string verificationToken, CancellationToken cancellationToken = default);
        Task<IEnumerable<User>> GetByRoleAsync(string role, CancellationToken cancellationToken = default);
        Task<IEnumerable<User>> GetActiveUsersAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<User>> GetByRegionIdAsync(ObjectId regionId, CancellationToken cancellationToken = default);
        Task<IEnumerable<User>> GetByTMIdAsync(ObjectId tmId, CancellationToken cancellationToken = default);
        Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
        Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default);
        Task<bool> UpdateLastLoginAsync(ObjectId userId, DateTime loginDate, CancellationToken cancellationToken = default);
        Task<bool> UpdateRefreshTokenAsync(ObjectId userId, RefreshToken refreshToken, CancellationToken cancellationToken = default);
        Task<bool> RevokeRefreshTokenAsync(ObjectId userId, string refreshToken, string revokedByIp, CancellationToken cancellationToken = default);
        Task<bool> RevokeAllUserRefreshTokensAsync(ObjectId userId, string revokedByIp, CancellationToken cancellationToken = default);
        Task<bool> CleanupExpiredRefreshTokensAsync(ObjectId userId, int retentionDays = 30, CancellationToken cancellationToken = default);
        Task<int> CleanupAllExpiredRefreshTokensAsync(int retentionDays = 30, CancellationToken cancellationToken = default);
    }
}