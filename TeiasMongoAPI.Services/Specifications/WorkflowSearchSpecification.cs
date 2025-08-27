using System.Linq.Expressions;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Core.Specifications;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;

namespace TeiasMongoAPI.Services.Specifications
{
    public class WorkflowSearchSpecification : BaseSpecification<Workflow>
    {
        public WorkflowSearchSpecification(string searchTerm, PaginationRequestDto pagination)
            : base(CreateSearchCriteria(searchTerm))
        {
            // Set default ordering by CreatedAt descending
            AddOrderByDescending(w => w.CreatedAt);
            
            // Apply pagination
            ApplyPaging(
                skip: (pagination.PageNumber - 1) * pagination.PageSize,
                take: pagination.PageSize
            );
        }

        private static Expression<Func<Workflow, bool>> CreateSearchCriteria(string searchTerm)
        {
            if (string.IsNullOrEmpty(searchTerm))
            {
                // If no search term, return all workflows
                return w => true;
            }

            // Search in Name, Description, and Tags
            return w => w.Name.Contains(searchTerm) ||
                       w.Description.Contains(searchTerm) ||
                       w.Tags.Any(t => t.Contains(searchTerm));
        }
    }
}