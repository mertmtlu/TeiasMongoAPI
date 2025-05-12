using MongoDB.Driver;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Data.Context;
using TeiasMongoAPI.Data.Repositories.Implementations;

namespace TeiasMongoAPI.Data.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly MongoDbContext _context;
        private IClientSessionHandle? _session;
        private bool _disposed;

        private IUserRepository? _users;
        private IClientRepository? _clients;
        private IRegionRepository? _regions;
        private ITMRepository? _tms;
        private IBuildingRepository? _buildings;
        private IAlternativeTMRepository? _alternativeTMs;
        private IConcreteRepository? _concreteBlocks;
        private IMasonryRepository? _masonryBlocks;

        public UnitOfWork(MongoDbContext context)
        {
            _context = context;
        }

        public IUserRepository Users => _users ??= new UserRepository(_context);
        public IClientRepository Clients => _clients ??= new ClientRepository(_context);
        public IRegionRepository Regions => _regions ??= new RegionRepository(_context);
        public ITMRepository TMs => _tms ??= new TMRepository(_context);
        public IBuildingRepository Buildings => _buildings ??= new BuildingRepository(_context);
        public IAlternativeTMRepository AlternativeTMs => _alternativeTMs ??= new AlternativeTMRepository(_context);
        public IConcreteRepository ConcreteBlocks => _concreteBlocks ??= new ConcreteRepository(_context);
        public IMasonryRepository MasonryBlocks => _masonryBlocks ??= new MasonryRepository(_context);

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // MongoDB doesn't have a SaveChanges concept like EF Core
            // This method is here for interface compatibility
            return await Task.FromResult(0);
        }

        public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_session == null)
            {
                var client = _context.Database.Client;
                _session = await client.StartSessionAsync(cancellationToken: cancellationToken);
                _session.StartTransaction();
            }
        }

        public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_session != null && _session.IsInTransaction)
            {
                await _session.CommitTransactionAsync(cancellationToken);
            }
        }

        public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_session != null && _session.IsInTransaction)
            {
                await _session.AbortTransactionAsync(cancellationToken);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _session?.Dispose();
                }
                _disposed = true;
            }
        }
    }
}