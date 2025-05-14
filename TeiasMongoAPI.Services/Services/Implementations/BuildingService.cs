using AutoMapper;
using DnsClient.Internal;
using Microsoft.Extensions.Logging;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.Building;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Request.Search;
using TeiasMongoAPI.Services.DTOs.Response.Building;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.Interfaces;
using TeiasMongoAPI.Services.Services.Base;

namespace TeiasMongoAPI.Services.Services.Implementations
{
    public class BuildingService : BaseService, IBuildingService
    {
        private readonly IBlockService _blockService;

        public BuildingService(IUnitOfWork unitOfWork, IMapper mapper, IBlockService blockService, ILogger<BuildingService> logger)
            : base(unitOfWork, mapper, logger)
        {
            _blockService = blockService;
        }

        public async Task<BuildingDetailResponseDto> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var building = await _unitOfWork.Buildings.GetByIdAsync(objectId, cancellationToken);

            if (building == null)
            {
                throw new KeyNotFoundException($"Building with ID {id} not found.");
            }

            var dto = _mapper.Map<BuildingDetailResponseDto>(building);

            // Get TM info
            var tm = await _unitOfWork.TMs.GetByIdAsync(building.TmID, cancellationToken);
            dto.TM = _mapper.Map<TeiasMongoAPI.Services.DTOs.Response.TM.TMSummaryResponseDto>(tm);

            // Map blocks
            dto.Blocks = _mapper.Map<List<TeiasMongoAPI.Services.DTOs.Response.Block.BlockResponseDto>>(building.Blocks);
            dto.BlockCount = building.Blocks.Count;

            return dto;
        }

        public async Task<PagedResponse<BuildingListResponseDto>> GetAllAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var buildings = await _unitOfWork.Buildings.GetAllAsync(cancellationToken);
            var buildingsList = buildings.ToList();

            // Apply pagination
            var totalCount = buildingsList.Count;
            var paginatedBuildings = buildingsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = new List<BuildingListResponseDto>();
            foreach (var building in paginatedBuildings)
            {
                var dto = _mapper.Map<BuildingListResponseDto>(building);

                // Get TM name
                var tm = await _unitOfWork.TMs.GetByIdAsync(building.TmID, cancellationToken);
                dto.TmName = tm?.Name ?? "Unknown";

                dtos.Add(dto);
            }

            return new PagedResponse<BuildingListResponseDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<PagedResponse<BuildingListResponseDto>> GetByTmIdAsync(string tmId, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var tmObjectId = ParseObjectId(tmId);
            var buildings = await _unitOfWork.Buildings.GetByTmIdAsync(tmObjectId, cancellationToken);
            var buildingsList = buildings.ToList();

            // Apply pagination
            var totalCount = buildingsList.Count;
            var paginatedBuildings = buildingsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = new List<BuildingListResponseDto>();
            var tm = await _unitOfWork.TMs.GetByIdAsync(tmObjectId, cancellationToken);

            foreach (var building in paginatedBuildings)
            {
                var dto = _mapper.Map<BuildingListResponseDto>(building);
                dto.TmName = tm?.Name ?? "Unknown";
                dtos.Add(dto);
            }

            return new PagedResponse<BuildingListResponseDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<PagedResponse<BuildingListResponseDto>> SearchAsync(BuildingSearchDto searchDto, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var allBuildings = await _unitOfWork.Buildings.GetAllAsync(cancellationToken);
            var filteredBuildings = allBuildings.AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(searchDto.Name))
            {
                filteredBuildings = filteredBuildings.Where(b => b.Name.Contains(searchDto.Name, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(searchDto.TmId))
            {
                var tmId = ParseObjectId(searchDto.TmId);
                filteredBuildings = filteredBuildings.Where(b => b.TmID == tmId);
            }

            if (searchDto.Type.HasValue)
            {
                filteredBuildings = filteredBuildings.Where(b => b.Type == searchDto.Type.Value);
            }

            if (searchDto.InScopeOfMETU.HasValue)
            {
                filteredBuildings = filteredBuildings.Where(b => b.InScopeOfMETU == searchDto.InScopeOfMETU.Value);
            }

            if (!string.IsNullOrEmpty(searchDto.ReportName))
            {
                filteredBuildings = filteredBuildings.Where(b => b.ReportName.Contains(searchDto.ReportName, StringComparison.OrdinalIgnoreCase));
            }

            var buildingsList = filteredBuildings.ToList();

            // Apply pagination
            var totalCount = buildingsList.Count;
            var paginatedBuildings = buildingsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = new List<BuildingListResponseDto>();
            foreach (var building in paginatedBuildings)
            {
                var dto = _mapper.Map<BuildingListResponseDto>(building);

                // Get TM name
                var tm = await _unitOfWork.TMs.GetByIdAsync(building.TmID, cancellationToken);
                dto.TmName = tm?.Name ?? "Unknown";

                dtos.Add(dto);
            }

            return new PagedResponse<BuildingListResponseDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<BuildingResponseDto> CreateAsync(BuildingCreateDto dto, CancellationToken cancellationToken = default)
        {
            // Validate TM exists
            var tmId = ParseObjectId(dto.TmId);
            var tm = await _unitOfWork.TMs.GetByIdAsync(tmId, cancellationToken);
            if (tm == null)
            {
                throw new InvalidOperationException($"TM with ID {dto.TmId} not found.");
            }

            // Check if building with same BuildingTMID exists for this TM
            var existingBuildings = await _unitOfWork.Buildings.GetByTmIdAsync(tmId, cancellationToken);
            if (existingBuildings.Any(b => b.BuildingTMID == dto.BuildingTMID))
            {
                throw new InvalidOperationException($"Building with BuildingTMID {dto.BuildingTMID} already exists for TM {dto.TmId}.");
            }

            var building = _mapper.Map<Building>(dto);
            var createdBuilding = await _unitOfWork.Buildings.CreateAsync(building, cancellationToken);

            return _mapper.Map<BuildingResponseDto>(createdBuilding);
        }

        public async Task<BuildingResponseDto> UpdateAsync(string id, BuildingUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var existingBuilding = await _unitOfWork.Buildings.GetByIdAsync(objectId, cancellationToken);

            if (existingBuilding == null)
            {
                throw new KeyNotFoundException($"Building with ID {id} not found.");
            }

            // If updating TM, validate it exists
            if (!string.IsNullOrEmpty(dto.TmId))
            {
                var tmId = ParseObjectId(dto.TmId);
                var tm = await _unitOfWork.TMs.GetByIdAsync(tmId, cancellationToken);
                if (tm == null)
                {
                    throw new InvalidOperationException($"TM with ID {dto.TmId} not found.");
                }
            }

            // If updating BuildingTMID, check uniqueness within the TM
            if (dto.BuildingTMID.HasValue && dto.BuildingTMID.Value != existingBuilding.BuildingTMID)
            {
                var tmId = !string.IsNullOrEmpty(dto.TmId) ? ParseObjectId(dto.TmId) : existingBuilding.TmID;
                var buildingsInTM = await _unitOfWork.Buildings.GetByTmIdAsync(tmId, cancellationToken);
                if (buildingsInTM.Any(b => b.BuildingTMID == dto.BuildingTMID.Value && b._ID != objectId))
                {
                    throw new InvalidOperationException($"Building with BuildingTMID {dto.BuildingTMID} already exists for this TM.");
                }
            }

            _mapper.Map(dto, existingBuilding);
            var success = await _unitOfWork.Buildings.UpdateAsync(objectId, existingBuilding, cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException($"Failed to update building with ID {id}.");
            }

            return _mapper.Map<BuildingResponseDto>(existingBuilding);
        }

        public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var building = await _unitOfWork.Buildings.GetByIdAsync(objectId, cancellationToken);

            if (building == null)
            {
                throw new KeyNotFoundException($"Building with ID {id} not found.");
            }

            // Check if building has blocks
            if (building.Blocks.Any())
            {
                throw new InvalidOperationException($"Cannot delete building with ID {id} because it has {building.Blocks.Count} blocks. Remove all blocks first.");
            }

            return await _unitOfWork.Buildings.DeleteAsync(objectId, cancellationToken);
        }

        public async Task<BuildingResponseDto> AddBlockAsync(string buildingId, BuildingBlockAddDto dto, CancellationToken cancellationToken = default)
        {
            var buildingObjectId = ParseObjectId(buildingId);
            var building = await _unitOfWork.Buildings.GetByIdAsync(buildingObjectId, cancellationToken);

            if (building == null)
            {
                throw new KeyNotFoundException($"Building with ID {buildingId} not found.");
            }

            // Check if block already exists in the building
            var blockId = dto.BlockId;
            if (building.Blocks.Any(b => b.ID == blockId))
            {
                throw new InvalidOperationException($"Block with ID {blockId} already exists in the building.");
            }

            // The block should already exist and be retrieved from the BlockService
            // In a real implementation, you might have a separate endpoint to create blocks first
            // For now, we'll throw a NotImplementedException
            throw new NotImplementedException("Block creation/retrieval logic needs to be implemented. Blocks should be created using the BlockService first.");
        }

        public async Task<BuildingResponseDto> RemoveBlockAsync(string buildingId, BuildingBlockRemoveDto dto, CancellationToken cancellationToken = default)
        {
            var buildingObjectId = ParseObjectId(buildingId);
            var building = await _unitOfWork.Buildings.GetByIdAsync(buildingObjectId, cancellationToken);

            if (building == null)
            {
                throw new KeyNotFoundException($"Building with ID {buildingId} not found.");
            }

            var blockId = dto.BlockId;
            var blockExists = building.Blocks.Any(b => b.ID == blockId);

            if (!blockExists)
            {
                throw new KeyNotFoundException($"Block with ID {blockId} not found in the building.");
            }

            var success = await _unitOfWork.Buildings.RemoveBlockAsync(buildingObjectId, blockId, cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException($"Failed to remove block with ID {blockId} from building.");
            }

            var updatedBuilding = await _unitOfWork.Buildings.GetByIdAsync(buildingObjectId, cancellationToken);
            return _mapper.Map<BuildingResponseDto>(updatedBuilding);
        }

        public async Task<BuildingStatisticsResponseDto> GetStatisticsAsync(string buildingId, CancellationToken cancellationToken = default)
        {
            var building = await GetByIdAsync(buildingId, cancellationToken);

            return new BuildingStatisticsResponseDto
            {
                BuildingId = buildingId,
                BlockCount = building.BlockCount,
                ConcreteBlockCount = building.Blocks.Count(b => b.ModelingType == "Concrete"),
                MasonryBlockCount = building.Blocks.Count(b => b.ModelingType == "Masonry"),
                TotalArea = building.Blocks.Sum(b => b.XAxisLength * b.YAxisLength),
                MaxHeight = building.Blocks.Any() ? building.Blocks.Max(b => b.TotalHeight) : 0,
                Code = building.Code,
                BKS = building.BKS
            };
        }
    }
}