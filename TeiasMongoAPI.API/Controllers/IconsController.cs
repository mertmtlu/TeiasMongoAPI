using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using TeiasMongoAPI.API.Controllers.Base;
using TeiasMongoAPI.Core.Models.Common;
using TeiasMongoAPI.Services.DTOs.Request.Icon;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.DTOs.Response.Icon;
using TeiasMongoAPI.Services.Interfaces;

namespace TeiasMongoAPI.API.Controllers
{
    [Authorize]
    public class IconsController : BaseController
    {
        private readonly IIconService _iconService;

        public IconsController(IIconService iconService, ILogger<IconsController> logger) : base(logger)
        {
            _iconService = iconService;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<IconResponseDto>>> CreateIcon([FromBody] IconCreateDto createDto, CancellationToken cancellationToken = default)
        {
            var validationResult = ValidateModelState<IconResponseDto>();
            if (validationResult != null) return validationResult;

            if (CurrentUserId == null)
                return Unauthorized<IconResponseDto>("User not authenticated");

            try
            {
                var result = await _iconService.CreateIconAsync(createDto, CurrentUserId.Value.ToString(), cancellationToken);
                return Created(result, $"/api/icons/{result.Id}", "Icon created successfully");
            }
            catch (ArgumentException ex)
            {
                return ValidationError<IconResponseDto>(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict<IconResponseDto>(ex.Message);
            }
            catch (Exception ex)
            {
                return HandleException<IconResponseDto>(ex, "creating icon");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<IconResponseDto>>> GetIcon(string id, CancellationToken cancellationToken = default)
        {
            var parseResult = ParseObjectId(id);
            if (parseResult != null) return parseResult.Result;
            
            if (!ObjectId.TryParse(id, out var objectId))
                return ValidationError<IconResponseDto>("Invalid ID format");

            try
            {
                var result = await _iconService.GetIconByIdAsync(objectId, cancellationToken);
                return result != null ? Success(result) : NotFound<IconResponseDto>("Icon not found");
            }
            catch (Exception ex)
            {
                return HandleException<IconResponseDto>(ex, "retrieving icon");
            }
        }

        [HttpGet("entity/{entityType}/{entityId}")]
        public async Task<ActionResult<ApiResponse<IconResponseDto>>> GetIconByEntity(IconEntityType entityType, string entityId, CancellationToken cancellationToken = default)
        {
            var parseResult = ParseObjectId(entityId, "entityId");
            if (parseResult != null) return parseResult.Result;
            
            if (!ObjectId.TryParse(entityId, out var objectId))
                return ValidationError<IconResponseDto>("Invalid entity ID format");

            try
            {
                var result = await _iconService.GetIconByEntityAsync(entityType, objectId, cancellationToken);
                return result != null ? Success(result) : NotFound<IconResponseDto>("Icon not found for entity");
            }
            catch (Exception ex)
            {
                return HandleException<IconResponseDto>(ex, "retrieving icon by entity");
            }
        }

        [HttpGet("type/{entityType}")]
        public async Task<ActionResult<ApiResponse<IEnumerable<IconResponseDto>>>> GetIconsByType(IconEntityType entityType, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _iconService.GetIconsByEntityTypeAsync(entityType, cancellationToken);
                return Success(result);
            }
            catch (Exception ex)
            {
                return HandleException<IEnumerable<IconResponseDto>>(ex, "retrieving icons by type");
            }
        }

        [HttpPost("batch")]
        public async Task<ActionResult<ApiResponse<IEnumerable<IconResponseDto>>>> GetIconsBatch([FromBody] IconBatchRequestDto batchRequest, CancellationToken cancellationToken = default)
        {
            var validationResult = ValidateModelState<IEnumerable<IconResponseDto>>();
            if (validationResult != null) return validationResult;

            try
            {
                var iconIds = batchRequest.IconIds?.Select(ObjectId.Parse) ?? Enumerable.Empty<ObjectId>();
                var result = await _iconService.GetIconsByIdsAsync(iconIds, cancellationToken);
                return Success(result);
            }
            catch (FormatException)
            {
                return ValidationError<IEnumerable<IconResponseDto>>("Invalid icon ID format in batch request");
            }
            catch (Exception ex)
            {
                return HandleException<IEnumerable<IconResponseDto>>(ex, "retrieving icons batch");
            }
        }

        [HttpPost("batch/entities")]
        public async Task<ActionResult<ApiResponse<IEnumerable<IconResponseDto>>>> GetIconsByEntityIds([FromBody] IconEntityBatchRequestDto batchRequest, CancellationToken cancellationToken = default)
        {
            var validationResult = ValidateModelState<IEnumerable<IconResponseDto>>();
            if (validationResult != null) return validationResult;

            try
            {
                var entityIds = batchRequest.EntityIds?.Select(ObjectId.Parse) ?? Enumerable.Empty<ObjectId>();
                var result = await _iconService.GetIconsByEntityIdsAsync(batchRequest.EntityType, entityIds, cancellationToken);
                return Success(result);
            }
            catch (FormatException)
            {
                return ValidationError<IEnumerable<IconResponseDto>>("Invalid entity ID format in batch request");
            }
            catch (Exception ex)
            {
                return HandleException<IEnumerable<IconResponseDto>>(ex, "retrieving icons by entity IDs");
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponse<IconResponseDto>>> UpdateIcon(string id, [FromBody] IconUpdateDto updateDto, CancellationToken cancellationToken = default)
        {
            var validationResult = ValidateModelState<IconResponseDto>();
            if (validationResult != null) return validationResult;

            var parseResult = ParseObjectId(id);
            if (parseResult != null) return parseResult.Result;
            
            if (!ObjectId.TryParse(id, out var objectId))
                return ValidationError<IconResponseDto>("Invalid ID format");

            if (CurrentUserId == null)
                return Unauthorized<IconResponseDto>("User not authenticated");

            try
            {
                var result = await _iconService.UpdateIconAsync(objectId, updateDto, CurrentUserId.Value.ToString(), cancellationToken);
                return result != null ? Success(result, "Icon updated successfully") : NotFound<IconResponseDto>("Icon not found");
            }
            catch (UnauthorizedAccessException)
            {
                return Forbidden<IconResponseDto>("You don't have permission to update this icon");
            }
            catch (Exception ex)
            {
                return HandleException<IconResponseDto>(ex, "updating icon");
            }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteIcon(string id, CancellationToken cancellationToken = default)
        {
            var parseResult = ParseObjectId(id);
            if (parseResult != null) return parseResult.Result;
            
            if (!ObjectId.TryParse(id, out var objectId))
                return ValidationError<object>("Invalid ID format");

            if (CurrentUserId == null)
                return Unauthorized<object>("User not authenticated");

            try
            {
                var result = await _iconService.DeleteIconAsync(objectId, CurrentUserId.Value.ToString(), cancellationToken);
                return result ? Success<object>(null, "Icon deleted successfully") : NotFound<object>("Icon not found");
            }
            catch (UnauthorizedAccessException)
            {
                return Forbidden<object>("You don't have permission to delete this icon");
            }
            catch (Exception ex)
            {
                return HandleException<object>(ex, "deleting icon");
            }
        }

        [HttpDelete("entity/{entityType}/{entityId}")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteIconByEntity(IconEntityType entityType, string entityId, CancellationToken cancellationToken = default)
        {
            var parseResult = ParseObjectId(entityId, "entityId");
            if (parseResult != null) return parseResult.Result;
            
            if (!ObjectId.TryParse(entityId, out var objectId))
                return ValidationError<object>("Invalid entity ID format");

            if (CurrentUserId == null)
                return Unauthorized<object>("User not authenticated");

            try
            {
                var result = await _iconService.DeleteIconByEntityAsync(entityType, objectId, CurrentUserId.Value.ToString(), cancellationToken);
                return result ? Success<object>(null, "Icon deleted successfully") : NotFound<object>("Icon not found for entity");
            }
            catch (UnauthorizedAccessException)
            {
                return Forbidden<object>("You don't have permission to delete this icon");
            }
            catch (Exception ex)
            {
                return HandleException<object>(ex, "deleting icon by entity");
            }
        }

        [HttpGet("user")]
        public async Task<ActionResult<ApiResponse<IEnumerable<IconResponseDto>>>> GetUserIcons(CancellationToken cancellationToken = default)
        {
            if (CurrentUserId == null)
                return Unauthorized<IEnumerable<IconResponseDto>>("User not authenticated");

            try
            {
                var result = await _iconService.GetUserIconsAsync(CurrentUserId.Value.ToString(), cancellationToken);
                return Success(result);
            }
            catch (Exception ex)
            {
                return HandleException<IEnumerable<IconResponseDto>>(ex, "retrieving user icons");
            }
        }

        [HttpGet("stats/{entityType}")]
        public async Task<ActionResult<ApiResponse<IconStatsResponseDto>>> GetIconStats(IconEntityType entityType, CancellationToken cancellationToken = default)
        {
            try
            {
                var count = await _iconService.GetIconCountByTypeAsync(entityType, cancellationToken);
                var stats = new IconStatsResponseDto
                {
                    EntityType = entityType,
                    TotalCount = count
                };
                return Success(stats);
            }
            catch (Exception ex)
            {
                return HandleException<IconStatsResponseDto>(ex, "retrieving icon statistics");
            }
        }

        [HttpPost("validate")]
        public async Task<ActionResult<ApiResponse<IconValidationResponseDto>>> ValidateIconConstraints([FromBody] IconValidationRequestDto validationRequest, CancellationToken cancellationToken = default)
        {
            var validationResult = ValidateModelState<IconValidationResponseDto>();
            if (validationResult != null) return validationResult;

            try
            {
                var entityId = ObjectId.Parse(validationRequest.EntityId);
                ObjectId? excludeIconId = null;
                
                if (!string.IsNullOrEmpty(validationRequest.ExcludeIconId))
                {
                    excludeIconId = ObjectId.Parse(validationRequest.ExcludeIconId);
                }

                var isValid = await _iconService.ValidateIconConstraintsAsync(validationRequest.EntityType, entityId, excludeIconId, cancellationToken);
                
                var response = new IconValidationResponseDto
                {
                    IsValid = isValid,
                    Message = isValid ? "Icon constraints satisfied" : "Icon constraints violated"
                };

                return Success(response);
            }
            catch (FormatException)
            {
                return ValidationError<IconValidationResponseDto>("Invalid ID format in validation request");
            }
            catch (Exception ex)
            {
                return HandleException<IconValidationResponseDto>(ex, "validating icon constraints");
            }
        }
    }
}