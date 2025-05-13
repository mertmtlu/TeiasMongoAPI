using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeiasMongoAPI.API.Attributes;
using TeiasMongoAPI.API.Controllers.Base;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.AlternativeTM;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Response.AlternativeTM;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.Interfaces;

namespace TeiasMongoAPI.API.Controllers
{
    [Microsoft.AspNetCore.Mvc.ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AlternativeTMsController : BaseController
    {
        private readonly IAlternativeTMService _alternativeTMService;

        public AlternativeTMsController(
            IAlternativeTMService alternativeTMService,
            ILogger<AlternativeTMsController> logger)
            : base(logger)
        {
            _alternativeTMService = alternativeTMService;
        }

        /// <summary>
        /// Get alternative TM by ID
        /// </summary>
        [HttpGet("{id}")]
        [RequirePermission(UserPermissions.ViewAlternativeTMs)]
        public async Task<ActionResult<ApiResponse<AlternativeTMDetailDto>>> GetById(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _alternativeTMService.GetByIdAsync(id, cancellationToken);
            }, $"Get alternative TM {id}");
        }

        /// <summary>
        /// Get alternative TMs by TM ID
        /// </summary>
        [HttpGet("by-tm/{tmId}")]
        [RequirePermission(UserPermissions.ViewAlternativeTMs)]
        public async Task<ActionResult<ApiResponse<PagedResponse<AlternativeTMSummaryDto>>>> GetByTmId(
            string tmId,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(tmId);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _alternativeTMService.GetByTmIdAsync(tmId, pagination, cancellationToken);
            }, $"Get alternative TMs for TM {tmId}");
        }

        /// <summary>
        /// Get alternative TMs by city
        /// </summary>
        [HttpGet("by-city/{city}")]
        [RequirePermission(UserPermissions.ViewAlternativeTMs)]
        public async Task<ActionResult<ApiResponse<PagedResponse<AlternativeTMSummaryDto>>>> GetByCity(
            string city,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _alternativeTMService.GetByCityAsync(city, pagination, cancellationToken);
            }, $"Get alternative TMs in city {city}");
        }

        /// <summary>
        /// Get alternative TMs by county
        /// </summary>
        [HttpGet("by-county/{county}")]
        [RequirePermission(UserPermissions.ViewAlternativeTMs)]
        public async Task<ActionResult<ApiResponse<PagedResponse<AlternativeTMSummaryDto>>>> GetByCounty(
            string county,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _alternativeTMService.GetByCountyAsync(county, pagination, cancellationToken);
            }, $"Get alternative TMs in county {county}");
        }

        /// <summary>
        /// Create new alternative TM
        /// </summary>
        [HttpPost]
        [RequirePermission(UserPermissions.CreateAlternativeTMs)]
        [AuditLog("CreateAlternativeTM")]
        public async Task<ActionResult<ApiResponse<AlternativeTMDto>>> Create(
            [FromBody] AlternativeTMCreateDto dto,
            CancellationToken cancellationToken = default)
        {
            // Validate model state
            var validationResult = ValidateModelState<AlternativeTMDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _alternativeTMService.CreateAsync(dto, cancellationToken);
            }, "Create alternative TM");
        }

        /// <summary>
        /// Update alternative TM
        /// </summary>
        [HttpPut("{id}")]
        [RequirePermission(UserPermissions.UpdateAlternativeTMs)]
        [AuditLog("UpdateAlternativeTM")]
        public async Task<ActionResult<ApiResponse<AlternativeTMDto>>> Update(
            string id,
            [FromBody] AlternativeTMUpdateDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<AlternativeTMDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _alternativeTMService.UpdateAsync(id, dto, cancellationToken);
            }, $"Update alternative TM {id}");
        }

        /// <summary>
        /// Delete alternative TM
        /// </summary>
        [HttpDelete("{id}")]
        [RequirePermission(UserPermissions.DeleteAlternativeTMs)]
        [AuditLog("DeleteAlternativeTM")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _alternativeTMService.DeleteAsync(id, cancellationToken);
            }, $"Delete alternative TM {id}");
        }

        /// <summary>
        /// Compare alternative TMs for a specific TM
        /// </summary>
        [HttpGet("compare/{tmId}")]
        [RequirePermission(UserPermissions.ViewAlternativeTMs)]
        public async Task<ActionResult<ApiResponse<List<AlternativeTMComparisonDto>>>> CompareAlternatives(
            string tmId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(tmId);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _alternativeTMService.CompareAlternativesAsync(tmId, cancellationToken);
            }, $"Compare alternatives for TM {tmId}");
        }

        /// <summary>
        /// Create alternative TM from existing TM
        /// </summary>
        [HttpPost("create-from-tm/{tmId}")]
        [RequirePermission(UserPermissions.CreateAlternativeTMs)]
        [AuditLog("CreateAlternativeTMFromTM")]
        public async Task<ActionResult<ApiResponse<AlternativeTMDto>>> CreateFromTM(
            string tmId,
            [FromBody] CreateFromTMDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(tmId);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync<AlternativeTMDto>(async () =>
            {
                // This would need to be implemented in the service
                throw new NotImplementedException("Create from TM functionality not yet implemented");
            }, $"Create alternative TM from TM {tmId}");
        }
    }
}