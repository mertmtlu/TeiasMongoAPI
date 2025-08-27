using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Core.Specifications;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;

namespace TeiasMongoAPI.Services.Specifications
{
    public class AllBuildingsSpecification : BaseSpecification<Building>
    {
        public AllBuildingsSpecification(PaginationRequestDto pagination)
            : base()
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