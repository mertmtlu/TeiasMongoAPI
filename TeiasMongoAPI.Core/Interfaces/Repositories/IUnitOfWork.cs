using System;
using System.Threading;
using System.Threading.Tasks;

namespace TeiasMongoAPI.Core.Interfaces.Repositories
{
    public interface IUnitOfWork : IDisposable
    {
        IUserRepository Users { get; }
        IClientRepository Clients { get; }
        IRegionRepository Regions { get; }
        ITMRepository TMs { get; }
        IBuildingRepository Buildings { get; }
        IAlternativeTMRepository AlternativeTMs { get; }
        
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
        Task BeginTransactionAsync(CancellationToken cancellationToken = default);
        Task CommitTransactionAsync(CancellationToken cancellationToken = default);
        Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
    }
}