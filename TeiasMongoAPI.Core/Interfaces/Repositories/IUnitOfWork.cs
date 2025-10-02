using System;
using System.Threading;
using System.Threading.Tasks;

namespace TeiasMongoAPI.Core.Interfaces.Repositories
{
    public interface IUnitOfWork : IDisposable
    {
        IUserRepository Users { get; }
        IGroupRepository Groups { get; }
        IClientRepository Clients { get; }
        IRegionRepository Regions { get; }
        ITMRepository TMs { get; }
        IBuildingRepository Buildings { get; }
        IAlternativeTMRepository AlternativeTMs { get; }
        IProgramRepository Programs { get; }
        IVersionRepository Versions { get; }
        IUiComponentRepository UiComponents { get; }
        IRequestRepository Requests { get; }
        IExecutionRepository Executions { get; }
        IWorkflowRepository Workflows { get; }
        IWorkflowExecutionRepository WorkflowExecutions { get; }
        IUIInteractionRepository UIInteractions { get; }
        IRemoteAppRepository RemoteApps { get; }
        IIconRepository Icons { get; }
        IDemoShowcaseRepository DemoShowcases { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
        Task BeginTransactionAsync(CancellationToken cancellationToken = default);
        Task CommitTransactionAsync(CancellationToken cancellationToken = default);
        Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
    }
}