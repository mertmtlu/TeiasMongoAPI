using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeiasMongoAPI.API.Attributes;
using TeiasMongoAPI.API.Controllers.Base;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.Client;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Response.Client;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.Interfaces;

namespace TeiasMongoAPI.API.Controllers
{
    [Microsoft.AspNetCore.Mvc.ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ClientsController : BaseController
    {
        private readonly IClientService _clientService;

        public ClientsController(
            IClientService clientService,
            ILogger<ClientsController> logger)
            : base(logger)
        {
            _clientService = clientService;
        }

        /// <summary>
        /// Get all clients with pagination
        /// </summary>
        [HttpGet]
        [RequirePermission(UserPermissions.ViewClients)]
        public async Task<ActionResult<ApiResponse<PagedResponse<ClientListDto>>>> GetAll(
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _clientService.GetAllAsync(pagination, cancellationToken);
            }, "Get all clients");
        }

        /// <summary>
        /// Get client by ID
        /// </summary>
        [HttpGet("{id}")]
        [RequirePermission(UserPermissions.ViewClients)]
        public async Task<ActionResult<ApiResponse<ClientDetailDto>>> GetById(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _clientService.GetByIdAsync(id, cancellationToken);
            }, $"Get client {id}");
        }

        /// <summary>
        /// Get client by name
        /// </summary>
        [HttpGet("by-name/{name}")]
        [RequirePermission(UserPermissions.ViewClients)]
        public async Task<ActionResult<ApiResponse<ClientDto>>> GetByName(
            string name,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _clientService.GetByNameAsync(name, cancellationToken);
            }, $"Get client by name {name}");
        }

        /// <summary>
        /// Create new client
        /// </summary>
        [HttpPost]
        [RequirePermission(UserPermissions.CreateClients)]
        [AuditLog("CreateClient")]
        public async Task<ActionResult<ApiResponse<ClientDto>>> Create(
            [FromBody] ClientCreateDto dto,
            CancellationToken cancellationToken = default)
        {
            // Validate model state
            var validationResult = ValidateModelState<ClientDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                var result = await _clientService.CreateAsync(dto, cancellationToken);
                return result;
            }, "Create client");
        }

        /// <summary>
        /// Update client
        /// </summary>
        [HttpPut("{id}")]
        [RequirePermission(UserPermissions.UpdateClients)]
        [AuditLog("UpdateClient")]
        public async Task<ActionResult<ApiResponse<ClientDto>>> Update(
            string id,
            [FromBody] ClientUpdateDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<ClientDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _clientService.UpdateAsync(id, dto, cancellationToken);
            }, $"Update client {id}");
        }

        /// <summary>
        /// Delete client
        /// </summary>
        [HttpDelete("{id}")]
        [RequirePermission(UserPermissions.DeleteClients)]
        [AuditLog("DeleteClient")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _clientService.DeleteAsync(id, cancellationToken);
            }, $"Delete client {id}");
        }

        /// <summary>
        /// Get client summary statistics
        /// </summary>
        [HttpGet("{id}/statistics")]
        [RequirePermission(UserPermissions.ViewClients)]
        public async Task<ActionResult<ApiResponse<ClientStatisticsDto>>> GetStatistics(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                var client = await _clientService.GetByIdAsync(id, cancellationToken);
                var stats = new ClientStatisticsDto
                {
                    ClientId = id,
                    RegionCount = client.RegionCount,
                    TotalTMs = client.Regions.Sum(r =>
                    {
                        // This would normally come from a more efficient aggregation query
                        return 0; // Placeholder
                    })
                };
                return stats;
            }, $"Get statistics for client {id}");
        }
    }
}