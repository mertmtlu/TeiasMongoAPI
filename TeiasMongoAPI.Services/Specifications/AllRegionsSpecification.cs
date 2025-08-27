using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Core.Specifications;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;

namespace TeiasMongoAPI.Services.Specifications
{
    public class AllRegionsSpecification : BaseSpecification<Region>
    {
        public AllRegionsSpecification(PaginationRequestDto pagination)
            : base()
        {
            // Set default ordering by RegionID ascending
            AddOrderBy(r => r.RegionID);
            
            // Apply pagination
            ApplyPaging(
                skip: (pagination.PageNumber - 1) * pagination.PageSize,
                take: pagination.PageSize
            );
        }
    }
}