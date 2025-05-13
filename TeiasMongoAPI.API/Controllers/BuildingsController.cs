using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeiasMongoAPI.API.Attributes;
using TeiasMongoAPI.API.Controllers.Base;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.Building;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Request.Search;
using TeiasMongoAPI.Services.DTOs.Response.Building;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.Interfaces;

namespace TeiasMongoAPI.API.Controllers
{
    [Microsoft.AspNetCore.Mvc.ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BuildingsController : BaseController
    {
        private readonly IBuildingService _buildingService;

        public BuildingsController(
            IBuildingService buildingService,
            ILogger<BuildingsController> logger)
            : base(logger)
        {
            _buildingService = buildingService;
        }

        /// <summary>
        /// Get all buildings with pagination
        /// </summary>
        [HttpGet]
        [RequirePermission(UserPermissions.ViewBuildings)]
        public async Task<ActionResult<ApiResponse<PagedResponse<BuildingListResponseDto>>>> GetAll(
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _buildingService.GetAllAsync(pagination, cancellationToken);
            }, "Get all buildings");
        }

        /// <summary>
        /// Get building by ID
        /// </summary>
        [HttpGet("{id}")]
        [RequirePermission(UserPermissions.ViewBuildings)]
        public async Task<ActionResult<ApiResponse<BuildingDetailResponseDto>>> GetById(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _buildingService.GetByIdAsync(id, cancellationToken);
            }, $"Get building {id}");
        }

        /// <summary>
        /// Get buildings by TM ID
        /// </summary>
        [HttpGet("by-tm/{tmId}")]
        [RequirePermission(UserPermissions.ViewBuildings)]
        public async Task<ActionResult<ApiResponse<PagedResponse<BuildingListResponseDto>>>> GetByTmId(
            string tmId,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(tmId);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _buildingService.GetByTmIdAsync(tmId, pagination, cancellationToken);
            }, $"Get buildings by TM {tmId}");
        }

        /// <summary>
        /// Search buildings
        /// </summary>
        [HttpPost("search")]
        [RequirePermission(UserPermissions.ViewBuildings)]
        public async Task<ActionResult<ApiResponse<PagedResponse<BuildingListResponseDto>>>> Search(
            [FromBody] BuildingSearchDto searchDto,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _buildingService.SearchAsync(searchDto, pagination, cancellationToken);
            }, "Search buildings");
        }

        /// <summary>
        /// Create new building
        /// </summary>
        [HttpPost]
        [RequirePermission(UserPermissions.CreateBuildings)]
        [AuditLog("CreateBuilding")]
        public async Task<ActionResult<ApiResponse<BuildingResponseDto>>> Create(
            [FromBody] BuildingCreateDto dto,
            CancellationToken cancellationToken = default)
        {
            // Validate model state
            var validationResult = ValidateModelState<BuildingResponseDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _buildingService.CreateAsync(dto, cancellationToken);
            }, "Create building");
        }

        /// <summary>
        /// Update building
        /// </summary>
        [HttpPut("{id}")]
        [RequirePermission(UserPermissions.UpdateBuildings)]
        [AuditLog("UpdateBuilding")]
        public async Task<ActionResult<ApiResponse<BuildingResponseDto>>> Update(
            string id,
            [FromBody] BuildingUpdateDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<BuildingResponseDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _buildingService.UpdateAsync(id, dto, cancellationToken);
            }, $"Update building {id}");
        }

        /// <summary>
        /// Add block to building
        /// </summary>
        [HttpPost("{id}/blocks")]
        [RequirePermission(UserPermissions.UpdateBuildings)]
        [AuditLog("AddBlockToBuilding")]
        public async Task<ActionResult<ApiResponse<BuildingResponseDto>>> AddBlock(
            string id,
            [FromBody] BuildingBlockAddDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<BuildingResponseDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _buildingService.AddBlockAsync(id, dto, cancellationToken);
            }, $"Add block to building {id}");
        }

        /// <summary>
        /// Remove block from building
        /// </summary>
        [HttpDelete("{id}/blocks/{blockId}")]
        [RequirePermission(UserPermissions.UpdateBuildings)]
        [AuditLog("RemoveBlockFromBuilding")]
        public async Task<ActionResult<ApiResponse<BuildingResponseDto>>> RemoveBlock(
            string id,
            string blockId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                var dto = new BuildingBlockRemoveDto { BlockId = blockId };
                return await _buildingService.RemoveBlockAsync(id, dto, cancellationToken);
            }, $"Remove block {blockId} from building {id}");
        }

        /// <summary>
        /// Delete building
        /// </summary>
        [HttpDelete("{id}")]
        [RequirePermission(UserPermissions.DeleteBuildings)]
        [AuditLog("DeleteBuilding")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _buildingService.DeleteAsync(id, cancellationToken);
            }, $"Delete building {id}");
        }

        /// <summary>
        /// Get buildings by type
        /// </summary>
        [HttpGet("by-type/{type}")]
        [RequirePermission(UserPermissions.ViewBuildings)]
        public async Task<ActionResult<ApiResponse<PagedResponse<BuildingListResponseDto>>>> GetByType(
            BuildingType type,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            var searchDto = new BuildingSearchDto { Type = type };

            return await ExecuteAsync(async () =>
            {
                return await _buildingService.SearchAsync(searchDto, pagination, cancellationToken);
            }, $"Get buildings by type {type}");
        }

        /// <summary>
        /// Get buildings in scope of METU
        /// </summary>
        [HttpGet("metu-scope")]
        [RequirePermission(UserPermissions.ViewBuildings)]
        public async Task<ActionResult<ApiResponse<PagedResponse<BuildingListResponseDto>>>> GetInMETUScope(
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            var searchDto = new BuildingSearchDto { InScopeOfMETU = true };

            return await ExecuteAsync(async () =>
            {
                return await _buildingService.SearchAsync(searchDto, pagination, cancellationToken);
            }, "Get buildings in METU scope");
        }

        /// <summary>
        /// Get building statistics
        /// </summary>
        [HttpGet("{id}/statistics")]
        [RequirePermission(UserPermissions.ViewBuildings)]
        public async Task<ActionResult<ApiResponse<BuildingStatisticsResponseDto>>> GetStatistics(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                var building = await _buildingService.GetByIdAsync(id, cancellationToken);
                var stats = new BuildingStatisticsResponseDto
                {
                    BuildingId = id,
                    BlockCount = building.BlockCount,
                    ConcreteBlockCount = building.Blocks.Count(b => b.ModelingType == "Concrete"),
                    MasonryBlockCount = building.Blocks.Count(b => b.ModelingType == "Masonry"),
                    TotalArea = building.Blocks.Sum(b => b.XAxisLength * b.YAxisLength),
                    MaxHeight = building.Blocks.Max(b => b.TotalHeight),
                    Code = building.Code,
                    BKS = building.BKS
                };
                return stats;
            }, $"Get statistics for building {id}");
        }
    }
}