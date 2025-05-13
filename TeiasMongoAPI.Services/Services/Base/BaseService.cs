using AutoMapper;
using MongoDB.Bson;
using TeiasMongoAPI.Core.Interfaces.Repositories;

namespace TeiasMongoAPI.Services.Services.Base
{
    public abstract class BaseService
    {
        protected readonly IUnitOfWork _unitOfWork;
        protected readonly IMapper _mapper;

        protected BaseService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
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