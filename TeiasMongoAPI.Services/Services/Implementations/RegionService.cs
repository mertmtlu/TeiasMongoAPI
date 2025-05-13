using AutoMapper;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Services.Interfaces;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Request.Region;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.DTOs.Response.Region;
using TeiasMongoAPI.Services.Services.Base;

namespace TeiasMongoAPI.Services.Services.Implementations
{
    public class RegionService : BaseService, IRegionService
    {
        public RegionService(IUnitOfWork unitOfWork, IMapper mapper)
            : base(unitOfWork, mapper)
        {
        }

        public async Task<RegionDetailDto> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var region = await _unitOfWork.Regions.GetByIdAsync(objectId, cancellationToken);

            if (region == null)
            {
                throw new KeyNotFoundException($"Region with ID {id} not found.");
            }

            var dto = _mapper.Map<RegionDetailDto>(region);

            // Get client info
            var client = await _unitOfWork.Clients.GetByIdAsync(region.ClientID, cancellationToken);
            dto.Client = _mapper.Map<TeiasMongoAPI.Services.DTOs.Response.Client.ClientSummaryDto>(client);

            // Get TMs
            var tms = await _unitOfWork.TMs.GetByRegionIdAsync(objectId, cancellationToken);
            dto.TMCount = tms.Count();
            dto.ActiveTMCount = tms.Count(tm => tm.State == TMState.Active);
            dto.TMs = _mapper.Map<List<TeiasMongoAPI.Services.DTOs.Response.TM.TMSummaryDto>>(tms);

            return dto;
        }

        public async Task<PagedResponse<RegionListDto>> GetAllAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var regions = await _unitOfWork.Regions.GetAllAsync(cancellationToken);
            var regionsList = regions.ToList();

            // Apply pagination
            var totalCount = regionsList.Count;
            var paginatedRegions = regionsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = new List<RegionListDto>();
            foreach (var region in paginatedRegions)
            {
                var dto = _mapper.Map<RegionListDto>(region);

                // Get client name
                var client = await _unitOfWork.Clients.GetByIdAsync(region.ClientID, cancellationToken);
                dto.ClientName = client?.Name ?? "Unknown";

                // Get TM counts
                var tms = await _unitOfWork.TMs.GetByRegionIdAsync(region._ID, cancellationToken);
                dto.TMCount = tms.Count();
                dto.ActiveTMCount = tms.Count(tm => tm.State == TMState.Active);

                dtos.Add(dto);
            }

            return new PagedResponse<RegionListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<PagedResponse<RegionListDto>> GetByClientIdAsync(string clientId, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
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

            var dtos = new List<RegionListDto>();
            var client = await _unitOfWork.Clients.GetByIdAsync(clientObjectId, cancellationToken);

            foreach (var region in paginatedRegions)
            {
                var dto = _mapper.Map<RegionListDto>(region);
                dto.ClientName = client?.Name ?? "Unknown";

                // Get TM counts
                var tms = await _unitOfWork.TMs.GetByRegionIdAsync(region._ID, cancellationToken);
                dto.TMCount = tms.Count();
                dto.ActiveTMCount = tms.Count(tm => tm.State == TMState.Active);

                dtos.Add(dto);
            }

            return new PagedResponse<RegionListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<RegionDto> CreateAsync(RegionCreateDto dto, CancellationToken cancellationToken = default)
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

            return _mapper.Map<RegionDto>(createdRegion);
        }

        public async Task<RegionDto> UpdateAsync(string id, RegionUpdateDto dto, CancellationToken cancellationToken = default)
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

            return _mapper.Map<RegionDto>(existingRegion);
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

        public async Task<RegionDto> GetByNoAsync(int regionNo, CancellationToken cancellationToken = default)
        {
            var region = await _unitOfWork.Regions.GetByNoAsync(regionNo, cancellationToken);

            if (region == null)
            {
                throw new KeyNotFoundException($"Region with number {regionNo} not found.");
            }

            return _mapper.Map<RegionDto>(region);
        }

        public async Task<RegionDto> UpdateCitiesAsync(string id, RegionCityUpdateDto dto, CancellationToken cancellationToken = default)
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

            return _mapper.Map<RegionDto>(region);
        }
    }
}

## Phase 1: Data Layer Implementation (Repository Pattern)

## Phase 2: Service Layer Implementation
### 2.1 DTOs (Data Transfer Objects)
### 2.2 AutoMapper Profiles
### 2.3 Service Interfaces
### 2.4 Service Implementations
### 2.5 FluentValidation Validators

## Phase 3: API Layer Implementation
### 3.1 Controllers
### 3.2 Middleware
### 3.3 API Configuration

## Phase 4: Common Layer Implementation
### 4.1 Custom Exceptions
### 4.2 Extension Methods
### 4.3 Constants and Helpers

## Phase 5: Advanced Features
### 5.1 Authentication & Authorization
### 5.2 Caching
### 5.3 Background Jobs
### 5.4 API Versioning

## Phase 6: Testing
### 6.1 Unit Tests
### 6.2 Integration Tests
### 6.3 Performance Tests

## Phase 7: Documentation & Deployment
### 7.1 Documentation
### 7.2 Deployment Preparation
### 7.3 Monitoring

## Key Considerations

### Architecture Principles
-Keep domain models in Core layer pure (no dependencies)
- Use DTOs for API communication
- Implement repository pattern for data access
- Use dependency injection throughout

### MongoDB Best Practices
- Use proper indexing strategies
- Implement pagination for list operations
- Handle ObjectId serialization properly
- Use transactions where needed

### API Design
- Follow RESTful conventions
- Use proper HTTP status codes
- Implement consistent error responses
- Version your API from the start

### Security
- Validate all inputs
- Implement proper authentication/authorization
- Use HTTPS in production
- Sanitize data before storage

### Performance
- Implement caching strategically
- Use async/await properly
- Optimize MongoDB queries
- Implement pagination