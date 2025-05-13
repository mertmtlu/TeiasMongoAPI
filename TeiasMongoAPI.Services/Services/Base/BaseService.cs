using AutoMapper;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using TeiasMongoAPI.Core.Interfaces.Repositories;

namespace TeiasMongoAPI.Services.Services.Base
{
    public abstract class BaseService
    {
        protected readonly IUnitOfWork _unitOfWork;
        protected readonly IMapper _mapper;
        protected readonly ILogger _logger;

        protected BaseService(IUnitOfWork unitOfWork, IMapper mapper, ILogger logger)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected ObjectId ParseObjectId(string id)
        {
            if (!ObjectId.TryParse(id, out var objectId))
            {
                throw new ArgumentException($"Invalid ID format: {id}");
            }
            return objectId;
        }

        protected async Task<bool> CheckExistsAsync<T>(IGenericRepository<T> repository, ObjectId id, CancellationToken cancellationToken = default)
            where T : Core.Models.Base.AEntityBase
        {
            return await repository.ExistsAsync(id, cancellationToken);
        }
    }
}