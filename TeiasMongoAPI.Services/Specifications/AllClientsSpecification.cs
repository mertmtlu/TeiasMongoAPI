using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Core.Specifications;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;

namespace TeiasMongoAPI.Services.Specifications
{
    public class AllClientsSpecification : BaseSpecification<Client>
    {
        public AllClientsSpecification(PaginationRequestDto pagination)
            : base()
        {
            // Set default ordering by Name ascending
            AddOrderBy(c => c.Name);
            
            // Apply pagination
            ApplyPaging(
                skip: (pagination.PageNumber - 1) * pagination.PageSize,
                take: pagination.PageSize
            );
        }
    }
}