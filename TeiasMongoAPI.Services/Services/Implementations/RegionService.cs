using AutoMapper;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Services.Interfaces;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Request.Region;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.DTOs.Response.Region;
using TeiasMongoAPI.Services.Services.Base;
using Microsoft.Extensions.Logging;

namespace TeiasMongoAPI.Services.Services.Implementations
{
    public class RegionService : BaseService, IRegionService
    {
        public RegionService(IUnitOfWork unitOfWork, IMapper mapper, ILogger<RegionService> logger)
            : base(unitOfWork, mapper, logger)
        {
        }

        public async Task<RegionDetailResponseDto> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var region = await _unitOfWork.Regions.GetByIdAsync(objectId, cancellationToken);

            if (region == null)
            {
                throw new KeyNotFoundException($"Region with ID {id} not found.");
            }

            var dto = _mapper.Map<RegionDetailResponseDto>(region);

            // Get client info
            var client = await _unitOfWork.Clients.GetByIdAsync(region.ClientID, cancellationToken);
            dto.Client = _mapper.Map<TeiasMongoAPI.Services.DTOs.Response.Client.ClientSummaryResponseDto>(client);

            // Get TMs
            var tms = await _unitOfWork.TMs.GetByRegionIdAsync(objectId, cancellationToken);
            dto.TMCount = tms.Count();
            dto.ActiveTMCount = tms.Count(tm => tm.State == TMState.Active);
            dto.TMs = _mapper.Map<List<TeiasMongoAPI.Services.DTOs.Response.TM.TMSummaryResponseDto>>(tms);

            return dto;
        }

        public async Task<PagedResponse<RegionListResponseDto>> GetAllAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var regions = await _unitOfWork.Regions.GetAllAsync(cancellationToken);
            var regionsList = regions.ToList();

            // Apply pagination
            var totalCount = regionsList.Count;
            var paginatedRegions = regionsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = new List<RegionListResponseDto>();
            foreach (var region in paginatedRegions)
            {
                var dto = _mapper.Map<RegionListResponseDto>(region);

                // Get client name
                var client = await _unitOfWork.Clients.GetByIdAsync(region.ClientID, cancellationToken);
                dto.ClientName = client?.Name ?? "Unknown";

                // Get TM counts
                var tms = await _unitOfWork.TMs.GetByRegionIdAsync(region._ID, cancellationToken);
                dto.TMCount = tms.Count();
                dto.ActiveTMCount = tms.Count(tm => tm.State == TMState.Active);

                dtos.Add(dto);
            }

            return new PagedResponse<RegionListResponseDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<PagedResponse<RegionListResponseDto>> GetByClientIdAsync(string clientId, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var clientObjectId = ParseObjectId(clientId);
            var regions = await _unitOfWork.Regions.GetByClientIdAsync(clientObjectId, cancellationToken);
            var regionsList = regions.ToList();

            // Apply pagination
            var totalCount = regionsList.Count;
            var paginatedRegions = regionsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = new List<RegionListResponseDto>();
            var client = await _unitOfWork.Clients.GetByIdAsync(clientObjectId, cancellationToken);

            foreach (var region in paginatedRegions)
            {
                var dto = _mapper.Map<RegionListResponseDto>(region);
                dto.ClientName = client?.Name ?? "Unknown";

                // Get TM counts
                var tms = await _unitOfWork.TMs.GetByRegionIdAsync(region._ID, cancellationToken);
                dto.TMCount = tms.Count();
                dto.ActiveTMCount = tms.Count(tm => tm.State == TMState.Active);

                dtos.Add(dto);
            }

            return new PagedResponse<RegionListResponseDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<RegionResponseDto> CreateAsync(RegionCreateDto dto, CancellationToken cancellationToken = default)
        {
            // Validate client exists
            var clientId = ParseObjectId(dto.ClientId);
            var client = await _unitOfWork.Clients.GetByIdAsync(clientId, cancellationToken);
            if (client == null)
            {
                throw new InvalidOperationException($"Client with ID {dto.ClientId} not found.");
            }

            // Check if region with same ID exists
            var existingRegion = await _unitOfWork.Regions.GetByNoAsync(dto.Id, cancellationToken);
            if (existingRegion != null)
            {
                throw new InvalidOperationException($"Region with ID {dto.Id} already exists.");
            }

            var region = _mapper.Map<Region>(dto);
            var createdRegion = await _unitOfWork.Regions.CreateAsync(region, cancellationToken);

            return _mapper.Map<RegionResponseDto>(createdRegion);
        }

        public async Task<RegionResponseDto> UpdateAsync(string id, RegionUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var existingRegion = await _unitOfWork.Regions.GetByIdAsync(objectId, cancellationToken);

            if (existingRegion == null)
            {
                throw new KeyNotFoundException($"Region with ID {id} not found.");
            }

            // If updating client, validate it exists
            if (!string.IsNullOrEmpty(dto.ClientId))
            {
                var clientId = ParseObjectId(dto.ClientId);
                var client = await _unitOfWork.Clients.GetByIdAsync(clientId, cancellationToken);
                if (client == null)
                {
                    throw new InvalidOperationException($"Client with ID {dto.ClientId} not found.");
                }
            }

            // If updating region ID, check uniqueness
            if (dto.Id.HasValue && dto.Id.Value != existingRegion.Id)
            {
                var regionWithSameId = await _unitOfWork.Regions.GetByNoAsync(dto.Id.Value, cancellationToken);
                if (regionWithSameId != null)
                {
                    throw new InvalidOperationException($"Region with ID {dto.Id} already exists.");
                }
            }

            _mapper.Map(dto, existingRegion);
            var success = await _unitOfWork.Regions.UpdateAsync(objectId, existingRegion, cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException($"Failed to update region with ID {id}.");
            }

            return _mapper.Map<RegionResponseDto>(existingRegion);
        }

        public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var region = await _unitOfWork.Regions.GetByIdAsync(objectId, cancellationToken);

            if (region == null)
            {
                throw new KeyNotFoundException($"Region with ID {id} not found.");
            }

            // Check if region has TMs
            var tms = await _unitOfWork.TMs.GetByRegionIdAsync(objectId, cancellationToken);
            if (tms.Any())
            {
                throw new InvalidOperationException($"Cannot delete region with ID {id} because it has {tms.Count()} associated TMs.");
            }

            return await _unitOfWork.Regions.DeleteAsync(objectId, cancellationToken);
        }

        public async Task<RegionResponseDto> GetByNoAsync(int regionNo, CancellationToken cancellationToken = default)
        {
            var region = await _unitOfWork.Regions.GetByNoAsync(regionNo, cancellationToken);

            if (region == null)
            {
                throw new KeyNotFoundException($"Region with number {regionNo} not found.");
            }

            return _mapper.Map<RegionResponseDto>(region);
        }

        public async Task<RegionResponseDto> UpdateCitiesAsync(string id, RegionCityUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var region = await _unitOfWork.Regions.GetByIdAsync(objectId, cancellationToken);

            if (region == null)
            {
                throw new KeyNotFoundException($"Region with ID {id} not found.");
            }

            switch (dto.Action)
            {
                case RegionCityUpdateDto.Operation.Add:
                    region.Cities.AddRange(dto.Cities.Where(c => !region.Cities.Contains(c)));
                    break;
                case RegionCityUpdateDto.Operation.Remove:
                    region.Cities.RemoveAll(c => dto.Cities.Contains(c));
                    break;
                default:
                    throw new InvalidOperationException($"Invalid operation: {dto.Action}");
            }

            var success = await _unitOfWork.Regions.UpdateAsync(objectId, region, cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException($"Failed to update cities for region with ID {id}.");
            }

            return _mapper.Map<RegionResponseDto>(region);
        }
    }
}