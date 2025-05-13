using AutoMapper;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.Interfaces;
using TeiasMongoAPI.Services.Services.Base;

namespace TeiasMongoAPI.Services.Services.Implementations
{
    public class RoleMigrationService : BaseService, IRoleMigrationService
    {
        public RoleMigrationService(IUnitOfWork unitOfWork, IMapper mapper)
            : base(unitOfWork, mapper)
        {
        }

        public async Task<int> MigrateUserRolesAsync(CancellationToken cancellationToken = default)
        {
            var users = await _unitOfWork.Users.GetAllAsync(cancellationToken);
            var migratedCount = 0;

            foreach (var user in users)
            {
                var needsMigration = user.Roles.Contains("RegionManager") ||
                                   user.Roles.Contains("TMOperator");

                if (!needsMigration)
                    continue;

                var newRoles = new List<string>();

                if (user.Roles.Contains("RegionManager"))
                {
                    newRoles.Add(UserRoles.Manager);
                }

                if (user.Roles.Contains("TMOperator"))
                {
                    newRoles.Add(UserRoles.Engineer);
                }

                // Keep other existing roles
                foreach (var role in user.Roles)
                {
                    if (role != "RegionManager" && role != "TMOperator")
                    {
                        newRoles.Add(role);
                    }
                }

                user.Roles = newRoles.Distinct().ToList();

                // Assign permissions based on new roles
                user.Permissions = RolePermissions.GetUserPermissions(user);

                var success = await _unitOfWork.Users.UpdateAsync(user._ID, user, cancellationToken);
                if (success)
                    migratedCount++;
            }

            return migratedCount;
        }

        public async Task<bool> IsMigrationNeededAsync(CancellationToken cancellationToken = default)
        {
            var users = await _unitOfWork.Users.GetAllAsync(cancellationToken);

            return users.Any(u => u.Roles.Contains("RegionManager") ||
                                 u.Roles.Contains("TMOperator"));
        }
    }
}