// File: TeiasMongoAPI.Services/Interfaces/IRoleMigrationService.cs

using System.Threading;
using System.Threading.Tasks;

namespace TeiasMongoAPI.Services.Interfaces
{
    public interface IRoleMigrationService
    {
        Task<int> MigrateUserRolesAsync(CancellationToken cancellationToken = default);
        Task<bool> IsMigrationNeededAsync(CancellationToken cancellationToken = default);
    }
}