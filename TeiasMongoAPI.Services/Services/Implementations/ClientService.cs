using AutoMapper;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Services.Interfaces;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.Client;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Response.Client;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.Services.Base;
using Microsoft.Extensions.Logging;

namespace TeiasMongoAPI.Services.Services.Implementations
{
    public class ClientService : BaseService, IClientService
    {
        public ClientService(IUnitOfWork unitOfWork, IMapper mapper, ILogger<ClientService> logger)
            : base(unitOfWork, mapper, logger)
        {
        }

        public async Task<ClientDetailResponseDto> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var client = await _unitOfWork.Clients.GetByIdAsync(objectId, cancellationToken);

            if (client == null)
            {
                throw new KeyNotFoundException($"Client with ID {id} not found.");
            }

            var dto = _mapper.Map<ClientDetailResponseDto>(client);

            // Get related regions
            var regions = await _unitOfWork.Regions.GetByClientIdAsync(objectId, cancellationToken);
            dto.RegionCount = regions.Count();
            dto.Regions = _mapper.Map<List<DTOs.Response.Region.RegionSummaryResponseDto>>(regions);

            return dto;
        }

        public async Task<PagedResponse<ClientListResponseDto>> GetAllAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var clients = await _unitOfWork.Clients.GetAllAsync(cancellationToken);
            var clientsList = clients.ToList();

            // Apply pagination
            var totalCount = clientsList.Count;
            var paginatedClients = clientsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = new List<ClientListResponseDto>();
            foreach (var client in paginatedClients)
            {
                var dto = _mapper.Map<ClientListResponseDto>(client);

                // Get region count
                var regions = await _unitOfWork.Regions.GetByClientIdAsync(client._ID, cancellationToken);
                dto.RegionCount = regions.Count();

                // Get total TM count
                var totalTMs = 0;
                foreach (var region in regions)
                {
                    var tms = await _unitOfWork.TMs.GetByRegionIdAsync(region._ID, cancellationToken);
                    totalTMs += tms.Count();
                }
                dto.TotalTMCount = totalTMs;

                dtos.Add(dto);
            }

            return new PagedResponse<ClientListResponseDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<ClientResponseDto> CreateAsync(ClientCreateDto dto, CancellationToken cancellationToken = default)
        {
            // Check if client with same name exists
            var existingClient = await _unitOfWork.Clients.GetByNameAsync(dto.Name, cancellationToken);
            if (existingClient != null)
            {
                throw new InvalidOperationException($"Client with name '{dto.Name}' already exists.");
            }

            var client = _mapper.Map<Client>(dto);
            var createdClient = await _unitOfWork.Clients.CreateAsync(client, cancellationToken);

            return _mapper.Map<ClientResponseDto>(createdClient);
        }

        public async Task<ClientResponseDto> UpdateAsync(string id, ClientUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var existingClient = await _unitOfWork.Clients.GetByIdAsync(objectId, cancellationToken);

            if (existingClient == null)
            {
                throw new KeyNotFoundException($"Client with ID {id} not found.");
            }

            // Check if new name already exists
            if (!string.IsNullOrEmpty(dto.Name) && dto.Name != existingClient.Name)
            {
                var clientWithSameName = await _unitOfWork.Clients.GetByNameAsync(dto.Name, cancellationToken);
                if (clientWithSameName != null)
                {
                    throw new InvalidOperationException($"Client with name '{dto.Name}' already exists.");
                }
            }

            _mapper.Map(dto, existingClient);
            var success = await _unitOfWork.Clients.UpdateAsync(objectId, existingClient, cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException($"Failed to update client with ID {id}.");
            }

            return _mapper.Map<ClientResponseDto>(existingClient);
        }

        public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var client = await _unitOfWork.Clients.GetByIdAsync(objectId, cancellationToken);

            if (client == null)
            {
                throw new KeyNotFoundException($"Client with ID {id} not found.");
            }

            // Check if client has regions
            var regions = await _unitOfWork.Regions.GetByClientIdAsync(objectId, cancellationToken);
            if (regions.Any())
            {
                throw new InvalidOperationException($"Cannot delete client with ID {id} because it has {regions.Count()} associated regions.");
            }

            return await _unitOfWork.Clients.DeleteAsync(objectId, cancellationToken);
        }

        public async Task<ClientResponseDto> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            var client = await _unitOfWork.Clients.GetByNameAsync(name, cancellationToken);

            if (client == null)
            {
                throw new KeyNotFoundException($"Client with name '{name}' not found.");
            }

            return _mapper.Map<ClientResponseDto>(client);
        }

        public async Task<ClientStatisticsResponseDto> GetStatisticsAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var client = await _unitOfWork.Clients.GetByIdAsync(objectId, cancellationToken);

            if (client == null)
            {
                throw new KeyNotFoundException($"Client with ID {id} not found.");
            }

            // Get regions
            var regions = await _unitOfWork.Regions.GetByClientIdAsync(objectId, cancellationToken);
            var regionsList = regions.ToList();

            // Calculate total TMs and buildings
            var totalTMs = 0;
            var totalBuildings = 0;
            var activeTMs = 0;

            foreach (var region in regionsList)
            {
                var tms = await _unitOfWork.TMs.GetByRegionIdAsync(region._ID, cancellationToken);
                var tmsList = tms.ToList();

                totalTMs += tmsList.Count;
                activeTMs += tmsList.Count(tm => tm.State == TMState.Active);

                // Count buildings for each TM
                foreach (var tm in tmsList)
                {
                    var buildings = await _unitOfWork.Buildings.GetByTmIdAsync(tm._ID, cancellationToken);
                    totalBuildings += buildings.Count();
                }
            }

            return new ClientStatisticsResponseDto
            {
                ClientId = id,
                RegionCount = regionsList.Count,
                TotalTMs = totalTMs,
                TotalBuildings = totalBuildings,
                ActiveTMs = activeTMs
            };
        }
    }
}