using MongoDB.Bson;
using MongoDB.Driver;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Data.Context;
using TeiasMongoAPI.Data.Repositories.Base;

namespace TeiasMongoAPI.Data.Repositories.Implementations
{
    public class UserRepository : GenericRepository<User>, IUserRepository
    {
        private readonly MongoDbContext _context;

        public UserRepository(MongoDbContext context) : base(context.Database)
        {
            _context = context;
        }

        public async Task<User> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .Find(u => u.Email == email)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<User> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .Find(u => u.Username == username)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<User> GetByEmailOrUsernameAsync(string emailOrUsername, CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .Find(u => u.Email == emailOrUsername || u.Username == emailOrUsername)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<User> GetByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .Find(u => u.RefreshTokens.Any(rt => rt.Token == refreshToken))
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<User> GetByPasswordResetTokenAsync(string resetToken, CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .Find(u => u.PasswordResetToken == resetToken && u.PasswordResetTokenExpiry > DateTime.UtcNow)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IEnumerable<User>> GetByRoleAsync(string role, CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .Find(u => u.Roles.Contains(role))
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<User>> GetActiveUsersAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .Find(u => u.IsActive)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<User>> GetByClientIdAsync(ObjectId clientId, CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .Find(u => u.AssignedClients.Contains(clientId))
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
        {
            var count = await _context.Users
                .CountDocumentsAsync(u => u.Email == email, cancellationToken: cancellationToken);
            return count > 0;
        }

        public async Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default)
        {
            var count = await _context.Users
                .CountDocumentsAsync(u => u.Username == username, cancellationToken: cancellationToken);
            return count > 0;
        }

        public async Task<bool> UpdateLastLoginAsync(ObjectId userId, DateTime loginDate, CancellationToken cancellationToken = default)
        {
            var update = Builders<User>.Update.Set(u => u.LastLoginDate, loginDate);
            var result = await _context.Users.UpdateOneAsync(
                u => u._ID == userId,
                update,
                cancellationToken: cancellationToken);

            return result.ModifiedCount > 0;
        }

        public async Task<bool> UpdateRefreshTokenAsync(ObjectId userId, RefreshToken refreshToken, CancellationToken cancellationToken = default)
        {
            var update = Builders<User>.Update.Push(u => u.RefreshTokens, refreshToken);
            var result = await _context.Users.UpdateOneAsync(
                u => u._ID == userId,
                update,
                cancellationToken: cancellationToken);

            return result.ModifiedCount > 0;
        }

        public async Task<bool> RevokeRefreshTokenAsync(ObjectId userId, string refreshToken, string revokedByIp, CancellationToken cancellationToken = default)
        {
            var user = await GetByIdAsync(userId, cancellationToken);
            if (user == null) return false;

            var token = user.RefreshTokens.FirstOrDefault(rt => rt.Token == refreshToken);
            if (token == null) return false;

            token.Revoked = DateTime.UtcNow;
            token.RevokedByIp = revokedByIp;

            return await UpdateAsync(userId, user, cancellationToken);
        }

        public async Task<bool> RevokeAllUserRefreshTokensAsync(ObjectId userId, string revokedByIp, CancellationToken cancellationToken = default)
        {
            var user = await GetByIdAsync(userId, cancellationToken);
            if (user == null) return false;

            foreach (var token in user.RefreshTokens.Where(rt => rt.IsActive))
            {
                token.Revoked = DateTime.UtcNow;
                token.RevokedByIp = revokedByIp;
            }

            return await UpdateAsync(userId, user, cancellationToken);
        }

        public async Task<bool> CleanupExpiredRefreshTokensAsync(ObjectId userId, int retentionDays = 30, CancellationToken cancellationToken = default)
        {
            var user = await GetByIdAsync(userId, cancellationToken);
            if (user == null) return false;

            var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
            var tokensBeforeCleanup = user.RefreshTokens.Count;

            // Keep active tokens and recently revoked tokens (for audit trail)
            user.RefreshTokens = user.RefreshTokens
                .Where(rt => rt.IsActive ||
                            (rt.Revoked.HasValue && rt.Revoked.Value > cutoffDate))
                .ToList();

            // Only update if tokens were actually removed
            if (tokensBeforeCleanup != user.RefreshTokens.Count)
            {
                return await UpdateAsync(userId, user, cancellationToken);
            }

            return true;
        }

        public async Task<int> CleanupAllExpiredRefreshTokensAsync(int retentionDays = 30, CancellationToken cancellationToken = default)
        {
            var filter = Builders<User>.Filter.Empty;
            var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
            var totalCleaned = 0;

            // Find users with expired tokens
            var usersWithTokens = await _context.Users
                .Find(filter)
                .ToListAsync(cancellationToken);

            foreach (var user in usersWithTokens)
            {
                var originalCount = user.RefreshTokens.Count;

                user.RefreshTokens = user.RefreshTokens
                    .Where(rt => rt.IsActive ||
                                (rt.Revoked.HasValue && rt.Revoked.Value > cutoffDate))
                    .ToList();

                if (originalCount != user.RefreshTokens.Count)
                {
                    await UpdateAsync(user._ID, user, cancellationToken);
                    totalCleaned++;
                }
            }

            return totalCleaned;
        }
    }
}