using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeiasMongoAPI.API.Attributes;
using TeiasMongoAPI.API.Controllers.Base;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Request.Region;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.DTOs.Response.Region;
using TeiasMongoAPI.Services.Interfaces;

namespace TeiasMongoAPI.API.Controllers
{
    [Microsoft.AspNetCore.Mvc.ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class RegionsController : BaseController
    {
        private readonly IRegionService _regionService;

        public RegionsController(
            IRegionService regionService,
            ILogger<RegionsController> logger)
            : base(logger)
        {
            _regionService = regionService;
        }

        /// <summary>
        /// Get all regions with pagination
        /// </summary>
        [HttpGet]
        [RequirePermission(UserPermissions.ViewRegions)]
        public async Task<ActionResult<ApiResponse<PagedResponse<RegionListResponseDto>>>> GetAll(
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _regionService.GetAllAsync(pagination, cancellationToken);
            }, "Get all regions");
        }

        /// <summary>
        /// Get region by ID
        /// </summary>
        [HttpGet("{id}")]
        [RequirePermission(UserPermissions.ViewRegions)]
        public async Task<ActionResult<ApiResponse<RegionDetailResponseDto>>> GetById(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _regionService.GetByIdAsync(id, cancellationToken);
            }, $"Get region {id}");
        }

        /// <summary>
        /// Get region by number
        /// </summary>
        [HttpGet("by-number/{regionNo}")]
        [RequirePermission(UserPermissions.ViewRegions)]
        public async Task<ActionResult<ApiResponse<RegionResponseDto>>> GetByNumber(
            int regionNo,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _regionService.GetByNoAsync(regionNo, cancellationToken);
            }, $"Get region by number {regionNo}");
        }

        /// <summary>
        /// Get regions by client ID
        /// </summary>
        [HttpGet("by-client/{clientId}")]
        [RequirePermission(UserPermissions.ViewRegions)]
        public async Task<ActionResult<ApiResponse<PagedResponse<RegionListResponseDto>>>> GetByClientId(
            string clientId,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(clientId);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _regionService.GetByClientIdAsync(clientId, pagination, cancellationToken);
            }, $"Get regions by client {clientId}");
        }

        /// <summary>
        /// Create new region
        /// </summary>
        [HttpPost]
        [RequirePermission(UserPermissions.CreateRegions)]
        [AuditLog("CreateRegion")]
        public async Task<ActionResult<ApiResponse<RegionResponseDto>>> Create(
            [FromBody] RegionCreateDto dto,
            CancellationToken cancellationToken = default)
        {
            // Validate model state
            var validationResult = ValidateModelState<RegionResponseDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _regionService.CreateAsync(dto, cancellationToken);
            }, "Create region");
        }

        /// <summary>
        /// Update region
        /// </summary>
        [HttpPut("{id}")]
        [RequirePermission(UserPermissions.UpdateRegions)]
        [AuditLog("UpdateRegion")]
        public async Task<ActionResult<ApiResponse<RegionResponseDto>>> Update(
            string id,
            [FromBody] RegionUpdateDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<RegionResponseDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _regionService.UpdateAsync(id, dto, cancellationToken);
            }, $"Update region {id}");
        }

        /// <summary>
        /// Update region cities
        /// </summary>
        [HttpPut("{id}/cities")]
        [RequirePermission(UserPermissions.UpdateRegions)]
        [AuditLog("UpdateRegionCities")]
        public async Task<ActionResult<ApiResponse<RegionResponseDto>>> UpdateCities(
            string id,
            [FromBody] RegionCityUpdateDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<RegionResponseDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _regionService.UpdateCitiesAsync(id, dto, cancellationToken);
            }, $"Update cities for region {id}");
        }

        /// <summary>
        /// Delete region
        /// </summary>
        [HttpDelete("{id}")]
        [RequirePermission(UserPermissions.DeleteRegions)]
        [AuditLog("DeleteRegion")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _regionService.DeleteAsync(id, cancellationToken);
            }, $"Delete region {id}");
        }

        /// <summary>
        /// Get region statistics
        /// </summary>
        [HttpGet("{id}/statistics")]
        [RequirePermission(UserPermissions.ViewRegions)]
        public async Task<ActionResult<ApiResponse<RegionStatisticsResponseDto>>> GetStatistics(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                var region = await _regionService.GetByIdAsync(id, cancellationToken);
                var stats = new RegionStatisticsResponseDto
                {
                    RegionId = id,
                    CityCount = region.Cities.Count,
                    TMCount = region.TMCount,
                    ActiveTMCount = region.ActiveTMCount,
                    BuildingCount = 0 // Would need additional aggregation
                };
                return stats;
            }, $"Get statistics for region {id}");
        }

        /// <summary>
        /// Get regions that operate in a specific city
        /// </summary>
        [HttpGet("in-city/{city}")]
        [RequirePermission(UserPermissions.ViewRegions)]
        public async Task<ActionResult<ApiResponse<List<RegionSummaryResponseDto>>>> GetRegionsInCity(
            string city,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                // This would need to be added to the service
                var regions = await _regionService.GetAllAsync(new PaginationRequestDto { PageSize = 100 }, cancellationToken);
                var regionsInCity = regions.Items
                    .Where(r => r.Cities.Contains(city, StringComparer.OrdinalIgnoreCase))
                    .Select(r => new RegionSummaryResponseDto
                    {
                        Id = r.Id,
                        RegionId = r.RegionId,
                        Headquarters = r.Headquarters,
                        CityCount = r.Cities.Count
                    })
                    .ToList();
                return regionsInCity;
            }, $"Get regions in city {city}");
        }
    }
}