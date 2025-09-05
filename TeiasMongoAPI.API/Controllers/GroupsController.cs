using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeiasMongoAPI.API.Attributes;
using TeiasMongoAPI.Core.Models.Common;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.Group;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.DTOs.Response.Group;
using TeiasMongoAPI.Services.Interfaces;

namespace TeiasMongoAPI.API.Controllers
{
    [Route("api/[controller]")]
    [Attributes.ApiController]
    [Authorize]
    public class GroupsController : ControllerBase
    {
        private readonly IGroupService _groupService;
        private readonly ILogger<GroupsController> _logger;

        public GroupsController(IGroupService groupService, ILogger<GroupsController> logger)
        {
            _groupService = groupService;
            _logger = logger;
        }

        [HttpGet]
        [RequirePermission(UserPermissions.GroupView)]
        public async Task<ActionResult<PagedResponse<GroupListDto>>> GetAll(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            CancellationToken cancellationToken = default)
        {
            var pagination = new PaginationRequestDto { PageNumber = pageNumber, PageSize = pageSize };
            var result = await _groupService.GetAllAsync(pagination, cancellationToken);
            return Ok(result);
        }

        [HttpPost("search")]
        [RequirePermission(UserPermissions.GroupView)]
        public async Task<ActionResult<PagedResponse<GroupListDto>>> Search(
            [FromBody] GroupSearchDto searchDto,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            CancellationToken cancellationToken = default)
        {
            var pagination = new PaginationRequestDto { PageNumber = pageNumber, PageSize = pageSize };
            var result = await _groupService.SearchAsync(searchDto, pagination, cancellationToken);
            return Ok(result);
        }

        [HttpGet("{id}")]
        [RequirePermission(UserPermissions.GroupView)]
        public async Task<ActionResult<GroupDto>> GetById(string id, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _groupService.GetByIdAsync(id, cancellationToken);
                return Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return NotFound($"Group with ID {id} not found");
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("active")]
        [RequirePermission(UserPermissions.GroupView)]
        public async Task<ActionResult<IEnumerable<GroupListDto>>> GetActive(CancellationToken cancellationToken = default)
        {
            var result = await _groupService.GetActiveGroupsAsync(cancellationToken);
            return Ok(result);
        }

        [HttpGet("creator/{creatorId}")]
        [RequirePermission(UserPermissions.GroupView)]
        public async Task<ActionResult<IEnumerable<GroupListDto>>> GetByCreator(string creatorId, CancellationToken cancellationToken = default)
        {
            var result = await _groupService.GetByCreatorAsync(creatorId, cancellationToken);
            return Ok(result);
        }

        [HttpGet("user/{userId}")]
        [RequirePermission(UserPermissions.GroupView)]
        public async Task<ActionResult<IEnumerable<GroupListDto>>> GetUserGroups(string userId, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _groupService.GetUserGroupsAsync(userId, cancellationToken);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        [RequirePermission(UserPermissions.GroupCreate)]
        public async Task<ActionResult<GroupDto>> Create([FromBody] GroupCreateDto dto, CancellationToken cancellationToken = default)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value;

                var result = await _groupService.CreateAsync(dto, userIdClaim, cancellationToken);
                return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
        }

        [HttpPut("{id}")]
        [RequirePermission(UserPermissions.GroupEdit)]
        public async Task<ActionResult<GroupDto>> Update(string id, [FromBody] GroupUpdateDto dto, CancellationToken cancellationToken = default)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var result = await _groupService.UpdateAsync(id, dto, cancellationToken);
                return Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return NotFound($"Group with ID {id} not found");
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
        }

        [HttpDelete("{id}")]
        [RequirePermission(UserPermissions.GroupDelete)]
        public async Task<ActionResult> Delete(string id, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _groupService.DeleteAsync(id, cancellationToken);
                if (!result)
                {
                    return NotFound($"Group with ID {id} not found");
                }

                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPatch("{id}/status")]
        [RequirePermission(UserPermissions.GroupEdit)]
        public async Task<ActionResult> UpdateStatus(string id, [FromBody] bool isActive, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _groupService.UpdateStatusAsync(id, isActive, cancellationToken);
                if (!result)
                {
                    return NotFound($"Group with ID {id} not found");
                }

                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // Member Management Endpoints

        [HttpPost("{id}/members")]
        [RequirePermission(UserPermissions.GroupMemberManage)]
        public async Task<ActionResult> AddMember(string id, [FromBody] string userId, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _groupService.AddMemberAsync(id, userId, cancellationToken);
                if (!result)
                {
                    return BadRequest("Failed to add member to group");
                }

                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{id}/members/{userId}")]
        [RequirePermission(UserPermissions.GroupMemberManage)]
        public async Task<ActionResult> RemoveMember(string id, string userId, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _groupService.RemoveMemberAsync(id, userId, cancellationToken);
                if (!result)
                {
                    return NotFound("Member not found in group or group not found");
                }

                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("{id}/members/batch")]
        [RequirePermission(UserPermissions.GroupMemberManage)]
        public async Task<ActionResult> AddMembers(string id, [FromBody] List<string> userIds, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _groupService.AddMembersAsync(id, userIds, cancellationToken);
                return Ok(new { Success = result, Message = result ? "All members added successfully" : "Some members failed to be added" });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{id}/members/batch")]
        [RequirePermission(UserPermissions.GroupMemberManage)]
        public async Task<ActionResult> RemoveMembers(string id, [FromBody] List<string> userIds, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _groupService.RemoveMembersAsync(id, userIds, cancellationToken);
                return Ok(new { Success = result, Message = result ? "All members removed successfully" : "Some members failed to be removed" });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("{id}/members")]
        [RequirePermission(UserPermissions.GroupView)]
        public async Task<ActionResult<IEnumerable<GroupMemberDto>>> GetMembers(string id, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _groupService.GetMembersAsync(id, cancellationToken);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("{id}/members/{userId}/check")]
        [RequirePermission(UserPermissions.GroupView)]
        public async Task<ActionResult<bool>> CheckMembership(string id, string userId, CancellationToken cancellationToken = default)
        {
            var result = await _groupService.IsMemberAsync(id, userId, cancellationToken);
            return Ok(result);
        }
    }
}