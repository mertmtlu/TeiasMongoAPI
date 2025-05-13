using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeiasMongoAPI.API.Attributes;
using TeiasMongoAPI.API.Controllers.Base;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.Block;
using TeiasMongoAPI.Services.DTOs.Response.Block;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.Interfaces;
using System.Linq;

namespace TeiasMongoAPI.API.Controllers
{
    [Microsoft.AspNetCore.Mvc.ApiController]
    [Route("api/buildings/{buildingId}/blocks")]
    [Authorize]
    public class BlocksController : BaseController
    {
        private readonly IBlockService _blockService;

        public BlocksController(
            IBlockService blockService,
            ILogger<BlocksController> logger)
            : base(logger)
        {
            _blockService = blockService;
        }

        /// <summary>
        /// Get all blocks in a building
        /// </summary>
        [HttpGet]
        [RequirePermission(UserPermissions.ViewBlocks)]
        public async Task<ActionResult<ApiResponse<List<BlockResponseDto>>>> GetAll(
            string buildingId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(buildingId, "buildingId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _blockService.GetBlocksAsync(buildingId, cancellationToken);
            }, $"Get all blocks for building {buildingId}");
        }

        /// <summary>
        /// Get block by ID
        /// </summary>
        [HttpGet("{blockId}")]
        [RequirePermission(UserPermissions.ViewBlocks)]
        public async Task<ActionResult<ApiResponse<BlockResponseDto>>> GetById(
            string buildingId,
            string blockId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(buildingId, "buildingId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _blockService.GetBlockAsync(buildingId, blockId, cancellationToken);
            }, $"Get block {blockId} from building {buildingId}");
        }

        /// <summary>
        /// Get block summary
        /// </summary>
        [HttpGet("{blockId}/summary")]
        [RequirePermission(UserPermissions.ViewBlocks)]
        public async Task<ActionResult<ApiResponse<BlockSummaryResponseDto>>> GetSummary(
            string buildingId,
            string blockId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(buildingId, "buildingId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _blockService.GetBlockSummaryAsync(buildingId, blockId, cancellationToken);
            }, $"Get summary for block {blockId} from building {buildingId}");
        }

        /// <summary>
        /// Get all concrete blocks in a building
        /// </summary>
        [HttpGet("concrete")]
        [RequirePermission(UserPermissions.ViewBlocks)]
        public async Task<ActionResult<ApiResponse<List<ConcreteBlockResponseDto>>>> GetConcreteBlocks(
            string buildingId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(buildingId, "buildingId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _blockService.GetConcreteBlocksAsync(buildingId, cancellationToken);
            }, $"Get concrete blocks for building {buildingId}");
        }

        /// <summary>
        /// Get all masonry blocks in a building
        /// </summary>
        [HttpGet("masonry")]
        [RequirePermission(UserPermissions.ViewBlocks)]
        public async Task<ActionResult<ApiResponse<List<MasonryBlockResponseDto>>>> GetMasonryBlocks(
            string buildingId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(buildingId, "buildingId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _blockService.GetMasonryBlocksAsync(buildingId, cancellationToken);
            }, $"Get masonry blocks for building {buildingId}");
        }

        /// <summary>
        /// Create new concrete block
        /// </summary>
        [HttpPost("concrete")]
        [RequirePermission(UserPermissions.CreateBlocks)]
        [AuditLog("CreateConcreteBlock")]
        public async Task<ActionResult<ApiResponse<ConcreteBlockResponseDto>>> CreateConcrete(
            string buildingId,
            [FromBody] ConcreteCreateDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(buildingId, "buildingId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<ConcreteBlockResponseDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _blockService.CreateConcreteBlockAsync(buildingId, dto, cancellationToken);
            }, $"Create concrete block for building {buildingId}");
        }

        /// <summary>
        /// Create new masonry block
        /// </summary>
        [HttpPost("masonry")]
        [RequirePermission(UserPermissions.CreateBlocks)]
        [AuditLog("CreateMasonryBlock")]
        public async Task<ActionResult<ApiResponse<MasonryBlockResponseDto>>> CreateMasonry(
            string buildingId,
            [FromBody] MasonryCreateDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(buildingId, "buildingId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<MasonryBlockResponseDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _blockService.CreateMasonryBlockAsync(buildingId, dto, cancellationToken);
            }, $"Create masonry block for building {buildingId}");
        }

        /// <summary>
        /// Update concrete block
        /// </summary>
        [HttpPut("concrete/{blockId}")]
        [RequirePermission(UserPermissions.UpdateBlocks)]
        [AuditLog("UpdateConcreteBlock")]
        public async Task<ActionResult<ApiResponse<ConcreteBlockResponseDto>>> UpdateConcrete(
            string buildingId,
            string blockId,
            [FromBody] ConcreteUpdateDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(buildingId, "buildingId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<ConcreteBlockResponseDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _blockService.UpdateConcreteBlockAsync(buildingId, blockId, dto, cancellationToken);
            }, $"Update concrete block {blockId} in building {buildingId}");
        }

        /// <summary>
        /// Update masonry block
        /// </summary>
        [HttpPut("masonry/{blockId}")]
        [RequirePermission(UserPermissions.UpdateBlocks)]
        [AuditLog("UpdateMasonryBlock")]
        public async Task<ActionResult<ApiResponse<MasonryBlockResponseDto>>> UpdateMasonry(
            string buildingId,
            string blockId,
            [FromBody] MasonryUpdateDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(buildingId, "buildingId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<MasonryBlockResponseDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _blockService.UpdateMasonryBlockAsync(buildingId, blockId, dto, cancellationToken);
            }, $"Update masonry block {blockId} in building {buildingId}");
        }

        /// <summary>
        /// Delete block
        /// </summary>
        [HttpDelete("{blockId}")]
        [RequirePermission(UserPermissions.DeleteBlocks)]
        [AuditLog("DeleteBlock")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(
            string buildingId,
            string blockId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(buildingId, "buildingId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _blockService.DeleteBlockAsync(buildingId, blockId, cancellationToken);
            }, $"Delete block {blockId} from building {buildingId}");
        }

        /// <summary>
        /// Get block statistics
        /// </summary>
        [HttpGet("{blockId}/statistics")]
        [RequirePermission(UserPermissions.ViewBlocks)]
        public async Task<ActionResult<ApiResponse<BlockStatisticsResponseDto>>> GetStatistics(
            string buildingId,
            string blockId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(buildingId, "buildingId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                var block = await _blockService.GetBlockAsync(buildingId, blockId, cancellationToken);
                var stats = new BlockStatisticsResponseDto
                {
                    BlockId = blockId,
                    ModelingType = block.ModelingType,
                    Area = block.XAxisLength * block.YAxisLength,
                    Height = block.TotalHeight,
                    StoreyCount = block.StoreyHeight.Count,
                    AspectRatio = block.LongLength / block.ShortLength,
                    VolumeEstimate = block.XAxisLength * block.YAxisLength * block.TotalHeight
                };
                return stats;
            }, $"Get statistics for block {blockId} in building {buildingId}");
        }

        /// <summary>
        /// Copy block within the same building
        /// </summary>
        [HttpPost("{blockId}/copy")]
        [RequirePermission(UserPermissions.CreateBlocks)]
        [AuditLog("CopyBlock")]
        public async Task<ActionResult<ApiResponse<BlockResponseDto>>> CopyBlock(
            string buildingId,
            string blockId,
            [FromBody] CopyBlockDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(buildingId, "buildingId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // This is a simplified implementation - in a real system, you might want to add this to IBlockService
            return await ExecuteAsync(async () =>
            {
                var block = await _blockService.GetBlockAsync(buildingId, blockId, cancellationToken);

                // Create a copy with new ID
                if (block is ConcreteBlockResponseDto concreteBlockSource)
                {
                    var createDto = new ConcreteCreateDto
                    {
                        ID = dto.NewBlockId,
                        Name = dto.NewName ?? $"{block.Name} (Copy)",
                        XAxisLength = block.XAxisLength,
                        YAxisLength = block.YAxisLength,
                        StoreyHeight = block.StoreyHeight,
                        CompressiveStrengthOfConcrete = concreteBlockSource.CompressiveStrengthOfConcrete,
                        YieldStrengthOfSteel = concreteBlockSource.YieldStrengthOfSteel,
                        TransverseReinforcementSpacing = concreteBlockSource.TransverseReinforcementSpacing,
                        ReinforcementRatio = concreteBlockSource.ReinforcementRatio,
                        HookExists = concreteBlockSource.HookExists,
                        IsStrengthened = concreteBlockSource.IsStrengthened
                    };
                    var createdConcreteBlock = await _blockService.CreateConcreteBlockAsync(buildingId, createDto, cancellationToken);
                    // Cast ConcreteBlockDto to BlockDto
                    BlockResponseDto result = createdConcreteBlock;
                    return result;
                }
                else if (block is MasonryBlockResponseDto masonryBlockSource)
                {
                    var createDto = new MasonryCreateDto
                    {
                        ID = dto.NewBlockId,
                        Name = dto.NewName ?? $"{block.Name} (Copy)",
                        XAxisLength = block.XAxisLength,
                        YAxisLength = block.YAxisLength,
                        StoreyHeight = block.StoreyHeight,
                        // Note: UnitTypeList mapping would need proper implementation
                        // For now, creating empty list as MasonryUnitType is not fully defined
                        UnitTypeList = new List<TeiasMongoAPI.Core.Models.Block.MasonryUnitType>()
                    };
                    var createdMasonryBlock = await _blockService.CreateMasonryBlockAsync(buildingId, createDto, cancellationToken);
                    // Cast MasonryBlockDto to BlockDto
                    BlockResponseDto result = createdMasonryBlock;
                    return result;
                }
                else
                {
                    throw new InvalidOperationException($"Unknown block type: {block.ModelingType}");
                }
            }, $"Copy block {blockId} in building {buildingId}");
        }
    }
}