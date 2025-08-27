using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Core.Specifications;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;

namespace TeiasMongoAPI.Services.Specifications
{
    public class BuildingsByTmSpecification : BaseSpecification<Building>
    {
        public BuildingsByTmSpecification(ObjectId tmId, PaginationRequestDto pagination)
            : base(b => b.TmID == tmId)
        {
            // Set default ordering by Name ascending
            AddOrderBy(b => b.Name);
            
            // Apply pagination
            ApplyPaging(
                skip: (pagination.PageNumber - 1) * pagination.PageSize,
                take: pagination.PageSize
            );
        }
    }
}