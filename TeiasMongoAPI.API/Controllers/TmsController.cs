using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeiasMongoAPI.API.Attributes;
using TeiasMongoAPI.API.Controllers.Base;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Request.Search;
using TeiasMongoAPI.Services.DTOs.Request.TM;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.DTOs.Response.Hazard;
using TeiasMongoAPI.Services.DTOs.Response.TM;
using TeiasMongoAPI.Services.Interfaces;

namespace TeiasMongoAPI.API.Controllers
{
    [Microsoft.AspNetCore.Mvc.ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TMsController : BaseController
    {
        private readonly ITMService _tmService;

        public TMsController(
            ITMService tmService,
            ILogger<TMsController> logger)
            : base(logger)
        {
            _tmService = tmService;
        }

        /// <summary>
        /// Get all TMs with pagination
        /// </summary>
        [HttpGet]
        [RequirePermission(UserPermissions.ViewTMs)]
        public async Task<ActionResult<ApiResponse<PagedResponse<TMListResponseDto>>>> GetAll(
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _tmService.GetAllAsync(pagination, cancellationToken);
            }, "Get all TMs");
        }

        /// <summary>
        /// Get TM by ID
        /// </summary>
        [HttpGet("{id}")]
        [RequirePermission(UserPermissions.ViewTMs)]
        public async Task<ActionResult<ApiResponse<TMDetailResponseDto>>> GetById(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _tmService.GetByIdAsync(id, cancellationToken);
            }, $"Get TM {id}");
        }

        /// <summary>
        /// Get TM by name
        /// </summary>
        [HttpGet("by-name/{name}")]
        [RequirePermission(UserPermissions.ViewTMs)]
        public async Task<ActionResult<ApiResponse<TMResponseDto>>> GetByName(
            string name,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _tmService.GetByNameAsync(name, cancellationToken);
            }, $"Get TM by name {name}");
        }

        /// <summary>
        /// Get TMs by region ID
        /// </summary>
        [HttpGet("by-region/{regionId}")]
        [RequirePermission(UserPermissions.ViewTMs)]
        public async Task<ActionResult<ApiResponse<PagedResponse<TMListResponseDto>>>> GetByRegionId(
            string regionId,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(regionId);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _tmService.GetByRegionIdAsync(regionId, pagination, cancellationToken);
            }, $"Get TMs by region {regionId}");
        }

        /// <summary>
        /// Search TMs
        /// </summary>
        [HttpPost("search")]
        [RequirePermission(UserPermissions.ViewTMs)]
        public async Task<ActionResult<ApiResponse<PagedResponse<TMListResponseDto>>>> Search(
            [FromBody] TMSearchDto searchDto,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _tmService.SearchAsync(searchDto, pagination, cancellationToken);
            }, "Search TMs");
        }

        /// <summary>
        /// Create new TM
        /// </summary>
        [HttpPost]
        [RequirePermission(UserPermissions.CreateTMs)]
        [AuditLog("CreateTM")]
        public async Task<ActionResult<ApiResponse<TMResponseDto>>> Create(
            [FromBody] TMCreateDto dto,
            CancellationToken cancellationToken = default)
        {
            // Validate model state
            var validationResult = ValidateModelState<TMResponseDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _tmService.CreateAsync(dto, cancellationToken);
            }, "Create TM");
        }

        /// <summary>
        /// Update TM
        /// </summary>
        [HttpPut("{id}")]
        [RequirePermission(UserPermissions.UpdateTMs)]
        [AuditLog("UpdateTM")]
        public async Task<ActionResult<ApiResponse<TMResponseDto>>> Update(
            string id,
            [FromBody] TMUpdateDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<TMResponseDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _tmService.UpdateAsync(id, dto, cancellationToken);
            }, $"Update TM {id}");
        }

        /// <summary>
        /// Update TM state
        /// </summary>
        [HttpPut("{id}/state")]
        [RequirePermission(UserPermissions.UpdateTMs)]
        [AuditLog("UpdateTMState")]
        public async Task<ActionResult<ApiResponse<TMResponseDto>>> UpdateState(
            string id,
            [FromBody] TMStateUpdateDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<TMResponseDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _tmService.UpdateStateAsync(id, dto, cancellationToken);
            }, $"Update state for TM {id}");
        }

        /// <summary>
        /// Update TM voltages
        /// </summary>
        [HttpPut("{id}/voltages")]
        [RequirePermission(UserPermissions.UpdateTMs)]
        [AuditLog("UpdateTMVoltages")]
        public async Task<ActionResult<ApiResponse<TMResponseDto>>> UpdateVoltages(
            string id,
            [FromBody] TMVoltageUpdateDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<TMResponseDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _tmService.UpdateVoltagesAsync(id, dto, cancellationToken);
            }, $"Update voltages for TM {id}");
        }

        /// <summary>
        /// Delete TM
        /// </summary>
        [HttpDelete("{id}")]
        [RequirePermission(UserPermissions.DeleteTMs)]
        [AuditLog("DeleteTM")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _tmService.DeleteAsync(id, cancellationToken);
            }, $"Delete TM {id}");
        }

        /// <summary>
        /// Get TM statistics
        /// </summary>
        [HttpGet("{id}/statistics")]
        [RequirePermission(UserPermissions.ViewTMs)]
        public async Task<ActionResult<ApiResponse<TMStatisticsResponseDto>>> GetStatistics(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _tmService.GetStatisticsAsync(id, cancellationToken);
            }, $"Get statistics for TM {id}");
        }

        /// <summary>
        /// Get hazard summary for TM
        /// </summary>
        [HttpGet("{id}/hazard-summary")]
        [RequirePermission(UserPermissions.ViewTMs)]
        public async Task<ActionResult<ApiResponse<TMHazardSummaryResponseDto>>> GetHazardSummary(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _tmService.GetHazardSummaryAsync(id, cancellationToken);
            }, $"Get hazard summary for TM {id}");
        }
    }
}