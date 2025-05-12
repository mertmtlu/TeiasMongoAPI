using System;
using System.Threading.Tasks;

namespace TeiasMongoAPI.Core.Interfaces.Repositories
{
    public interface IUnitOfWork : IDisposable
    {
        IClientRepository Clients { get; }
        IRegionRepository Regions { get; }
        ITMRepository TMs { get; }
        IBuildingRepository Buildings { get; }
        IAlternativeTMRepository AlternativeTMs { get; }
        IConcreteRepository ConcreteBlocks { get; }
        IMasonryRepository MasonryBlocks { get; }

        Task<int> SaveChangesAsync();
    }
}