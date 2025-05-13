using AutoMapper;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Services.Interfaces;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.Client;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Response.Client;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.Services.Base;

namespace TeiasMongoAPI.Services.Services.Implementations
{
    public class ClientService : BaseService, IClientService
    {
        public ClientService(IUnitOfWork unitOfWork, IMapper mapper)
            : base(unitOfWork, mapper)
        {
        }

        public async Task<ClientDetailDto> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var client = await _unitOfWork.Clients.GetByIdAsync(objectId, cancellationToken);

            if (client == null)
            {
                throw new KeyNotFoundException($"Client with ID {id} not found.");
            }

            var dto = _mapper.Map<ClientDetailDto>(client);

            // Get related regions
            var regions = await _unitOfWork.Regions.GetByClientIdAsync(objectId, cancellationToken);
            dto.RegionCount = regions.Count();
            dto.Regions = _mapper.Map<List<TeiasMongoAPI.Services.DTOs.Response.Region.RegionSummaryDto>>(regions);

            return dto;
        }

        public async Task<PagedResponse<ClientListDto>> GetAllAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var clients = await _unitOfWork.Clients.GetAllAsync(cancellationToken);
            var clientsList = clients.ToList();

            // Apply pagination
            var totalCount = clientsList.Count;
            var paginatedClients = clientsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = new List<ClientListDto>();
            foreach (var client in paginatedClients)
            {
                var dto = _mapper.Map<ClientListDto>(client);

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

            return new PagedResponse<ClientListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<ClientDto> CreateAsync(ClientCreateDto dto, CancellationToken cancellationToken = default)
        {
            // Check if client with same name exists
            var existingClient = await _unitOfWork.Clients.GetByNameAsync(dto.Name, cancellationToken);
            if (existingClient != null)
            {
                throw new InvalidOperationException($"Client with name '{dto.Name}' already exists.");
            }

            var client = _mapper.Map<Client>(dto);
            var createdClient = await _unitOfWork.Clients.CreateAsync(client, cancellationToken);

            return _mapper.Map<ClientDto>(createdClient);
        }

        public async Task<ClientDto> UpdateAsync(string id, ClientUpdateDto dto, CancellationToken cancellationToken = default)
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

            return _mapper.Map<ClientDto>(existingClient);
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

        public async Task<ClientDto> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            var client = await _unitOfWork.Clients.GetByNameAsync(name, cancellationToken);

            if (client == null)
            {
                throw new KeyNotFoundException($"Client with name '{name}' not found.");
            }

            return _mapper.Map<ClientDto>(client);
        }
    }
}