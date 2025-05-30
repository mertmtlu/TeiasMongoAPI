using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeiasMongoAPI.API.Attributes;
using TeiasMongoAPI.API.Controllers.Base;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Response.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.Interfaces;

namespace TeiasMongoAPI.API.Controllers
{
    [Microsoft.AspNetCore.Mvc.ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class VersionsController : BaseController
    {
        private readonly IVersionService _versionService;

        public VersionsController(
            IVersionService versionService,
            ILogger<VersionsController> logger)
            : base(logger)
        {
            _versionService = versionService;
        }

        //#region Basic CRUD Operations

        ///// <summary>
        ///// Get all versions with pagination
        ///// </summary>
        //[HttpGet]
        //[RequirePermission(UserPermissions.ViewVersions)]
        //public async Task<ActionResult<ApiResponse<PagedResponse<VersionListDto>>>> GetAll(
        //    [FromQuery] PaginationRequestDto pagination,
        //    CancellationToken cancellationToken = default)
        //{
        //    return await ExecuteAsync(async () =>
        //    {
        //        return await _versionService.GetAllAsync(pagination, cancellationToken);
        //    }, "Get all versions");
        //}

        ///// <summary>
        ///// Get version by ID
        ///// </summary>
        //[HttpGet("{id}")]
        //[RequirePermission(UserPermissions.ViewVersions)]
        //public async Task<ActionResult<ApiResponse<VersionDetailDto>>> GetById(
        //    string id,
        //    CancellationToken cancellationToken = default)
        //{
        //    var objectIdResult = ParseObjectId(id);
        //    if (objectIdResult.Result != null) return objectIdResult.Result!;

        //    return await ExecuteAsync(async () =>
        //    {
        //        return await _versionService.GetByIdAsync(id, cancellationToken);
        //    }, $"Get version {id}");
        //}

        ///// <summary>
        ///// Create new version
        ///// </summary>
        //[HttpPost]
        //[RequirePermission(UserPermissions.CreateVersions)]
        //[AuditLog("CreateVersion")]
        //public async Task<ActionResult<ApiResponse<VersionDto>>> Create(
        //    [FromBody] VersionCreateDto dto,
        //    CancellationToken cancellationToken = default)
        //{
        //    // Validate model state
        //    var validationResult = ValidateModelState<VersionDto>();
        //    if (validationResult != null) return validationResult;

        //    return await ExecuteAsync(async () =>
        //    {
        //        return await _versionService.CreateAsync(dto, cancellationToken);
        //    }, "Create version");
        //}

        ///// <summary>
        ///// Update version
        ///// </summary>
        //[HttpPut("{id}")]
        //[RequirePermission(UserPermissions.UpdateVersions)]
        //[AuditLog("UpdateVersion")]
        //public async Task<ActionResult<ApiResponse<VersionDto>>> Update(
        //    string id,
        //    [FromBody] VersionUpdateDto dto,
        //    CancellationToken cancellationToken = default)
        //{
        //    var objectIdResult = ParseObjectId(id);
        //    if (objectIdResult.Result != null) return objectIdResult.Result!;

        //    // Validate model state
        //    var validationResult = ValidateModelState<VersionDto>();
        //    if (validationResult != null) return validationResult;

        //    return await ExecuteAsync(async () =>
        //    {
        //        return await _versionService.UpdateAsync(id, dto, cancellationToken);
        //    }, $"Update version {id}");
        //}

        ///// <summary>
        ///// Delete version
        ///// </summary>
        //[HttpDelete("{id}")]
        //[RequirePermission(UserPermissions.DeleteVersions)]
        //[AuditLog("DeleteVersion")]
        //public async Task<ActionResult<ApiResponse<bool>>> Delete(
        //    string id,
        //    CancellationToken cancellationToken = default)
        //{
        //    var objectIdResult = ParseObjectId(id);
        //    if (objectIdResult.Result != null) return objectIdResult.Result!;

        //    return await ExecuteAsync(async () =>
        //    {
        //        return await _versionService.DeleteAsync(id, cancellationToken);
        //    }, $"Delete version {id}");
        //}

        //#endregion

        //#region Version Management

        ///// <summary>
        ///// Advanced version search
        ///// </summary>
        //[HttpPost("search")]
        //[RequirePermission(UserPermissions.ViewVersions)]
        //public async Task<ActionResult<ApiResponse<PagedResponse<VersionListDto>>>> Search(
        //    [FromBody] VersionSearchDto searchDto,
        //    [FromQuery] PaginationRequestDto pagination,
        //    CancellationToken cancellationToken = default)
        //{
        //    return await ExecuteAsync(async () =>
        //    {
        //        return await _versionService.SearchAsync(searchDto, pagination, cancellationToken);
        //    }, "Search versions");
        //}

        ///// <summary>
        ///// Get versions by program
        ///// </summary>
        //[HttpGet("by-program/{programId}")]
        //[RequirePermission(UserPermissions.ViewVersions)]
        //public async Task<ActionResult<ApiResponse<PagedResponse<VersionListDto>>>> GetByProgram(
        //    string programId,
        //    [FromQuery] PaginationRequestDto pagination,
        //    CancellationToken cancellationToken = default)
        //{
        //    var objectIdResult = ParseObjectId(programId, "programId");
        //    if (objectIdResult.Result != null) return objectIdResult.Result!;

        //    return await ExecuteAsync(async () =>
        //    {
        //        return await _versionService.GetByProgramIdAsync(programId, pagination, cancellationToken);
        //    }, $"Get versions by program {programId}");
        //}

        ///// <summary>
        ///// Get versions by creator
        ///// </summary>
        //[HttpGet("by-creator/{creatorId}")]
        //[RequirePermission(UserPermissions.ViewVersions)]
        //public async Task<ActionResult<ApiResponse<PagedResponse<VersionListDto>>>> GetByCreator(
        //    string creatorId,
        //    [FromQuery] PaginationRequestDto pagination,
        //    CancellationToken cancellationToken = default)
        //{
        //    var objectIdResult = ParseObjectId(creatorId, "creatorId");
        //    if (objectIdResult.Result != null) return objectIdResult.Result!;

        //    return await ExecuteAsync(async () =>
        //    {
        //        return await _versionService.GetByCreatorAsync(creatorId, pagination, cancellationToken);
        //    }, $"Get versions by creator {creatorId}");
        //}

        ///// <summary>
        ///// Get versions by status
        ///// </summary>
        //[HttpGet("by-status/{status}")]
        //[RequirePermission(UserPermissions.ViewVersions)]
        //public async Task<ActionResult<ApiResponse<PagedResponse<VersionListDto>>>> GetByStatus(
        //    string status,
        //    [FromQuery] PaginationRequestDto pagination,
        //    CancellationToken cancellationToken = default)
        //{
        //    return await ExecuteAsync(async () =>
        //    {
        //        return await _versionService.GetByStatusAsync(status, pagination, cancellationToken);
        //    }, $"Get versions by status {status}");
        //}

        ///// <summary>
        ///// Get latest version for program
        ///// </summary>
        //[HttpGet("programs/{programId}/latest")]
        //[RequirePermission(UserPermissions.ViewVersions)]
        //public async Task<ActionResult<ApiResponse<VersionDto>>> GetLatestVersionForProgram(
        //    string programId,
        //    CancellationToken cancellationToken = default)
        //{
        //    var objectIdResult = ParseObjectId(programId, "programId");
        //    if (objectIdResult.Result != null) return objectIdResult.Result!;

        //    return await ExecuteAsync(async () =>
        //    {
        //        return await _versionService.GetLatestVersionForProgramAsync(programId, cancellationToken);
        //    }, $"Get latest version for program {programId}");
        //}

        ///// <summary>
        ///// Update version status
        ///// </summary>
        //[HttpPut("{id}/status")]
        //[RequirePermission(UserPermissions.UpdateVersions)]
        //[AuditLog("UpdateVersionStatus")]
        //public async Task<ActionResult<ApiResponse<bool>>> UpdateStatus(
        //    string id,
        //    [FromBody] VersionStatusUpdateDto dto,
        //    CancellationToken cancellationToken = default)
        //{
        //    var objectIdResult = ParseObjectId(id);
        //    if (objectIdResult.Result != null) return objectIdResult.Result!;

        //    // Validate model state
        //    var validationResult = ValidateModelState<bool>();
        //    if (validationResult != null) return validationResult;

        //    return await ExecuteAsync(async () =>
        //    {
        //        return await _versionService.UpdateStatusAsync(id, dto, cancellationToken);
        //    }, $"Update status for version {id}");
        //}

        //#endregion

        //#region Review Process

        ///// <summary>
        ///// Submit version for review
        ///// </summary>
        //[HttpPost("{id}/submit-review")]
        //[RequirePermission(UserPermissions.UpdateVersions)]
        //[AuditLog("SubmitVersionForReview")]
        //public async Task<ActionResult<ApiResponse<VersionReviewDto>>> SubmitForReview(
        //    string id,
        //    [FromBody] VersionReviewSubmissionDto dto,
        //    CancellationToken cancellationToken = default)
        //{
        //    var objectIdResult = ParseObjectId(id);
        //    if (objectIdResult.Result != null) return objectIdResult.Result!;

        //    // Validate model state
        //    var validationResult = ValidateModelState<VersionReviewDto>();
        //    if (validationResult != null) return validationResult;

        //    return await ExecuteAsync(async () =>
        //    {
        //        return await _versionService.SubmitReviewAsync(id, dto, cancellationToken);
        //    }, $"Submit version {id} for review");
        //}

        ///// <summary>
        ///// Approve version
        ///// </summary>
        //[HttpPost("{id}/approve")]
        //[RequirePermission(UserPermissions.ApproveVersions)]
        //[AuditLog("ApproveVersion")]
        //public async Task<ActionResult<ApiResponse<VersionReviewDto>>> ApproveVersion(
        //    string id,
        //    [FromBody] string comments,
        //    CancellationToken cancellationToken = default)
        //{
        //    var objectIdResult = ParseObjectId(id);
        //    if (objectIdResult.Result != null) return objectIdResult.Result!;

        //    if (string.IsNullOrWhiteSpace(comments))
        //    {
        //        return ValidationError<VersionReviewDto>("Comments are required for approval");
        //    }

        //    return await ExecuteAsync(async () =>
        //    {
        //        var reviewDto = new VersionReviewSubmissionDto
        //        {
        //            Status = "approved",
        //            Comments = comments
        //        };
        //        return await _versionService.SubmitReviewAsync(id, reviewDto, cancellationToken);
        //    }, $"Approve version {id}");
        //}

        ///// <summary>
        ///// Reject version
        ///// </summary>
        //[HttpPost("{id}/reject")]
        //[RequirePermission(UserPermissions.RejectVersions)]
        //[AuditLog("RejectVersion")]
        //public async Task<ActionResult<ApiResponse<VersionReviewDto>>> RejectVersion(
        //    string id,
        //    [FromBody] string comments,
        //    CancellationToken cancellationToken = default)
        //{
        //    var objectIdResult = ParseObjectId(id);
        //    if (objectIdResult.Result != null) return objectIdResult.Result!;

        //    if (string.IsNullOrWhiteSpace(comments))
        //    {
        //        return ValidationError<VersionReviewDto>("Comments are required for rejection");
        //    }

        //    return await ExecuteAsync(async () =>
        //    {
        //        var reviewDto = new VersionReviewSubmissionDto
        //        {
        //            Status = "rejected",
        //            Comments = comments
        //        };
        //        return await _versionService.SubmitReviewAsync(id, reviewDto, cancellationToken);
        //    }, $"Reject version {id}");
        //}

        ///// <summary>
        ///// Get version review history
        ///// </summary>
        //[HttpGet("{id}/review-history")]
        //[RequirePermission(UserPermissions.ViewVersions)]
        //public async Task<ActionResult<ApiResponse<List<VersionReviewDto>>>> GetReviewHistory(
        //    string id,
        //    CancellationToken cancellationToken = default)
        //{
        //    var objectIdResult = ParseObjectId(id);
        //    if (objectIdResult.Result != null) return objectIdResult.Result!;

        //    return await ExecuteAsync(async () =>
        //    {
        //        // Note: This method doesn't exist in IVersionService interface
        //        // You may need to add it or get it from the version detail
        //        var versionDetail = await _versionService.GetByIdAsync(id, cancellationToken);
        //        return new List<VersionReviewDto>(); // Placeholder - implement in service
        //    }, $"Get review history for version {id}");
        //}

        //#endregion

        //#region File Management

        ///// <summary>
        ///// Get version files
        ///// </summary>
        //[HttpGet("{id}/files")]
        //[RequirePermission(UserPermissions.ViewVersions)]
        //public async Task<ActionResult<ApiResponse<List<VersionFileDto>>>> GetFiles(
        //    string id,
        //    CancellationToken cancellationToken = default)
        //{
        //    var objectIdResult = ParseObjectId(id);
        //    if (objectIdResult.Result != null) return objectIdResult.Result!;

        //    return await ExecuteAsync(async () =>
        //    {
        //        return await _versionService.GetFilesByVersionIdAsync(id, cancellationToken);
        //    }, $"Get files for version {id}");
        //}

        ///// <summary>
        ///// Add files to version
        ///// </summary>
        //[HttpPost("{id}/files")]
        //[RequirePermission(UserPermissions.UpdateVersions)]
        //[AuditLog("AddVersionFiles")]
        //public async Task<ActionResult<ApiResponse<bool>>> AddFiles(
        //    string id,
        //    [FromBody] List<VersionFileCreateDto> files,
        //    CancellationToken cancellationToken = default)
        //{
        //    var objectIdResult = ParseObjectId(id);
        //    if (objectIdResult.Result != null) return objectIdResult.Result!;

        //    if (files == null || !files.Any())
        //    {
        //        return ValidationError<bool>("At least one file is required");
        //    }

        //    return await ExecuteAsync(async () =>
        //    {
        //        bool allSucceeded = true;
        //        foreach (var file in files)
        //        {
        //            var result = await _versionService.AddFileAsync(id, file, cancellationToken);
        //            if (!result)
        //            {
        //                allSucceeded = false;
        //                break;
        //            }
        //        }
        //        return allSucceeded;
        //    }, $"Add files to version {id}");
        //}

        ///// <summary>
        ///// Get version file content
        ///// </summary>
        //[HttpGet("{id}/files/{*filePath}")]
        //[RequirePermission(UserPermissions.ViewVersions)]
        //public async Task<ActionResult<ApiResponse<VersionFileDetailDto>>> GetFileContent(
        //    string id,
        //    string filePath,
        //    CancellationToken cancellationToken = default)
        //{
        //    var objectIdResult = ParseObjectId(id);
        //    if (objectIdResult.Result != null) return objectIdResult.Result!;

        //    if (string.IsNullOrWhiteSpace(filePath))
        //    {
        //        return ValidationError<VersionFileDetailDto>("File path is required");
        //    }

        //    return await ExecuteAsync(async () =>
        //    {
        //        return await _versionService.GetFileByPathAsync(id, filePath, cancellationToken);
        //    }, $"Get file content for version {id}, file {filePath}");
        //}

        ///// <summary>
        ///// Update version file
        ///// </summary>
        //[HttpPut("{id}/files/{*filePath}")]
        //[RequirePermission(UserPermissions.UpdateVersions)]
        //[AuditLog("UpdateVersionFile")]
        //public async Task<ActionResult<ApiResponse<bool>>> UpdateFile(
        //    string id,
        //    string filePath,
        //    [FromBody] VersionFileUpdateDto dto,
        //    CancellationToken cancellationToken = default)
        //{
        //    var objectIdResult = ParseObjectId(id);
        //    if (objectIdResult.Result != null) return objectIdResult.Result!;

        //    if (string.IsNullOrWhiteSpace(filePath))
        //    {
        //        return ValidationError<bool>("File path is required");
        //    }

        //    // Validate model state
        //    var validationResult = ValidateModelState<bool>();
        //    if (validationResult != null) return validationResult;

        //    return await ExecuteAsync(async () =>
        //    {
        //        return await _versionService.UpdateFileAsync(id, filePath, dto, cancellationToken);
        //    }, $"Update file for version {id}, file {filePath}");
        //}

        ///// <summary>
        ///// Delete version file
        ///// </summary>
        //[HttpDelete("{id}/files/{*filePath}")]
        //[RequirePermission(UserPermissions.UpdateVersions)]
        //[AuditLog("DeleteVersionFile")]
        //public async Task<ActionResult<ApiResponse<bool>>> DeleteFile(
        //    string id,
        //    string filePath,
        //    CancellationToken cancellationToken = default)
        //{
        //    var objectIdResult = ParseObjectId(id);
        //    if (objectIdResult.Result != null) return objectIdResult.Result!;

        //    if (string.IsNullOrWhiteSpace(filePath))
        //    {
        //        return ValidationError<bool>("File path is required");
        //    }

        //    return await ExecuteAsync(async () =>
        //    {
        //        return await _versionService.RemoveFileAsync(id, filePath, cancellationToken);
        //    }, $"Delete file for version {id}, file {filePath}");
        //}

        //#endregion

        //#region Deployment & Commits

        ///// <summary>
        ///// Deploy version
        ///// </summary>
        //[HttpPost("{id}/deploy")]
        //[RequirePermission(UserPermissions.DeployVersions)]
        //[AuditLog("DeployVersion")]
        //public async Task<ActionResult<ApiResponse<VersionDeploymentDto>>> DeployVersion(
        //    string id,
        //    [FromBody] VersionDeploymentRequestDto dto,
        //    CancellationToken cancellationToken = default)
        //{
        //    var objectIdResult = ParseObjectId(id);
        //    if (objectIdResult.Result != null) return objectIdResult.Result!;

        //    // Validate model state
        //    var validationResult = ValidateModelState<VersionDeploymentDto>();
        //    if (validationResult != null) return validationResult;

        //    return await ExecuteAsync(async () =>
        //    {
        //        return await _versionService.DeployVersionAsync(id, dto, cancellationToken);
        //    }, $"Deploy version {id}");
        //}

        ///// <summary>
        ///// Commit version changes
        ///// </summary>
        //[HttpPost("{id}/commit")]
        //[RequirePermission(UserPermissions.UpdateVersions)]
        //[AuditLog("CommitVersionChanges")]
        //public async Task<ActionResult<ApiResponse<VersionDto>>> CommitChanges(
        //    string id,
        //    [FromBody] VersionCommitDto dto,
        //    CancellationToken cancellationToken = default)
        //{
        //    var objectIdResult = ParseObjectId(id);
        //    if (objectIdResult.Result != null) return objectIdResult.Result!;

        //    // Validate model state
        //    var validationResult = ValidateModelState<VersionDto>();
        //    if (validationResult != null) return validationResult;

        //    return await ExecuteAsync(async () =>
        //    {
        //        // Note: The service method expects programId, but we have versionId
        //        // You may need to get the programId from the version first
        //        var version = await _versionService.GetByIdAsync(id, cancellationToken);
        //        // Assuming the version has a ProgramId property
        //        return await _versionService.CommitChangesAsync(version.ProgramId, dto, cancellationToken);
        //    }, $"Commit changes for version {id}");
        //}

        ///// <summary>
        ///// Validate commit
        ///// </summary>
        //[HttpPost("validate-commit")]
        //[RequirePermission(UserPermissions.ViewVersions)]
        //public async Task<ActionResult<ApiResponse<bool>>> ValidateCommit(
        //    [FromBody] VersionCommitValidationDto dto,
        //    CancellationToken cancellationToken = default)
        //{
        //    // Validate model state
        //    var validationResult = ValidateModelState<bool>();
        //    if (validationResult != null) return validationResult;

        //    return await ExecuteAsync(async () =>
        //    {
        //        // Note: This method is not in the service interface
        //        // You may need to add it or implement validation logic here
        //        return await _versionService.ValidateCommitAsync("", dto, cancellationToken);
        //    }, "Validate commit");
        //}

        //#endregion

        //#region Version Comparison

        ///// <summary>
        ///// Compare two versions
        ///// </summary>
        //[HttpGet("{id}/compare/{compareVersionId}")]
        //[RequirePermission(UserPermissions.ViewVersions)]
        //public async Task<ActionResult<ApiResponse<VersionDiffDto>>> CompareVersions(
        //    string id,
        //    string compareVersionId,
        //    CancellationToken cancellationToken = default)
        //{
        //    var objectIdResult = ParseObjectId(id);
        //    if (objectIdResult.Result != null) return objectIdResult.Result!;

        //    var compareObjectIdResult = ParseObjectId(compareVersionId, "compareVersionId");
        //    if (compareObjectIdResult.Result != null) return compareObjectIdResult.Result!;

        //    return await ExecuteAsync(async () =>
        //    {
        //        return await _versionService.GetDiffBetweenVersionsAsync(id, compareVersionId, cancellationToken);
        //    }, $"Compare versions {id} and {compareVersionId}");
        //}

        ///// <summary>
        ///// Get version diff
        ///// </summary>
        //[HttpGet("{id}/diff/{compareVersionId}")]
        //[RequirePermission(UserPermissions.ViewVersions)]
        //public async Task<ActionResult<ApiResponse<VersionDiffDto>>> GetVersionDiff(
        //    string id,
        //    string compareVersionId,
        //    CancellationToken cancellationToken = default)
        //{
        //    var objectIdResult = ParseObjectId(id);
        //    if (objectIdResult.Result != null) return objectIdResult.Result!;

        //    var compareObjectIdResult = ParseObjectId(compareVersionId, "compareVersionId");
        //    if (compareObjectIdResult.Result != null) return compareObjectIdResult.Result!;

        //    return await ExecuteAsync(async () =>
        //    {
        //        return await _versionService.GetDiffBetweenVersionsAsync(id, compareVersionId, cancellationToken);
        //    }, $"Get diff between versions {id} and {compareVersionId}");
        //}

        //#endregion

        //#region Additional Methods

        ///// <summary>
        ///// Get version by program and version number
        ///// </summary>
        //[HttpGet("programs/{programId}/version/{versionNumber}")]
        //[RequirePermission(UserPermissions.ViewVersions)]
        //public async Task<ActionResult<ApiResponse<VersionDto>>> GetByProgramAndVersionNumber(
        //    string programId,
        //    int versionNumber,
        //    CancellationToken cancellationToken = default)
        //{
        //    var objectIdResult = ParseObjectId(programId, "programId");
        //    if (objectIdResult.Result != null) return objectIdResult.Result!;

        //    if (versionNumber <= 0)
        //    {
        //        return ValidationError<VersionDto>("Version number must be greater than 0");
        //    }

        //    return await ExecuteAsync(async () =>
        //    {
        //        return await _versionService.GetByProgramIdAndVersionNumberAsync(programId, versionNumber, cancellationToken);
        //    }, $"Get version {versionNumber} for program {programId}");
        //}

        ///// <summary>
        ///// Get next version number for program
        ///// </summary>
        //[HttpGet("programs/{programId}/next-version-number")]
        //[RequirePermission(UserPermissions.ViewVersions)]
        //public async Task<ActionResult<ApiResponse<int>>> GetNextVersionNumber(
        //    string programId,
        //    CancellationToken cancellationToken = default)
        //{
        //    var objectIdResult = ParseObjectId(programId, "programId");
        //    if (objectIdResult.Result != null) return objectIdResult.Result!;

        //    return await ExecuteAsync(async () =>
        //    {
        //        return await _versionService.GetNextVersionNumberAsync(programId, cancellationToken);
        //    }, $"Get next version number for program {programId}");
        //}

        ///// <summary>
        ///// Get pending reviews
        ///// </summary>
        //[HttpGet("pending-reviews")]
        //[RequirePermission(UserPermissions.ViewVersions)]
        //public async Task<ActionResult<ApiResponse<PagedResponse<VersionListDto>>>> GetPendingReviews(
        //    [FromQuery] PaginationRequestDto pagination,
        //    CancellationToken cancellationToken = default)
        //{
        //    return await ExecuteAsync(async () =>
        //    {
        //        return await _versionService.GetPendingReviewsAsync(pagination, cancellationToken);
        //    }, "Get pending version reviews");
        //}

        ///// <summary>
        ///// Get versions by reviewer
        ///// </summary>
        //[HttpGet("by-reviewer/{reviewerId}")]
        //[RequirePermission(UserPermissions.ViewVersions)]
        //public async Task<ActionResult<ApiResponse<PagedResponse<VersionListDto>>>> GetByReviewer(
        //    string reviewerId,
        //    [FromQuery] PaginationRequestDto pagination,
        //    CancellationToken cancellationToken = default)
        //{
        //    var objectIdResult = ParseObjectId(reviewerId, "reviewerId");
        //    if (objectIdResult.Result != null) return objectIdResult.Result!;

        //    return await ExecuteAsync(async () =>
        //    {
        //        return await _versionService.GetByReviewerAsync(reviewerId, pagination, cancellationToken);
        //    }, $"Get versions by reviewer {reviewerId}");
        //}

        ///// <summary>
        ///// Get version statistics
        ///// </summary>
        //[HttpGet("programs/{programId}/stats")]
        //[RequirePermission(UserPermissions.ViewVersions)]
        //public async Task<ActionResult<ApiResponse<VersionStatsDto>>> GetVersionStats(
        //    string programId,
        //    CancellationToken cancellationToken = default)
        //{
        //    var objectIdResult = ParseObjectId(programId, "programId");
        //    if (objectIdResult.Result != null) return objectIdResult.Result!;

        //    return await ExecuteAsync(async () =>
        //    {
        //        return await _versionService.GetVersionStatsAsync(programId, cancellationToken);
        //    }, $"Get version statistics for program {programId}");
        //}

        ///// <summary>
        ///// Get version activity
        ///// </summary>
        //[HttpGet("programs/{programId}/activity")]
        //[RequirePermission(UserPermissions.ViewVersions)]
        //public async Task<ActionResult<ApiResponse<List<VersionActivityDto>>>> GetVersionActivity(
        //    string programId,
        //    [FromQuery] int days = 30,
        //    CancellationToken cancellationToken = default)
        //{
        //    var objectIdResult = ParseObjectId(programId, "programId");
        //    if (objectIdResult.Result != null) return objectIdResult.Result!;

        //    if (days <= 0)
        //    {
        //        return ValidationError<List<VersionActivityDto>>("Days must be greater than 0");
        //    }

        //    return await ExecuteAsync(async () =>
        //    {
        //        return await _versionService.GetVersionActivityAsync(programId, days, cancellationToken);
        //    }, $"Get version activity for program {programId}");
        //}

        ///// <summary>
        ///// Revert to previous version
        ///// </summary>
        //[HttpPost("programs/{programId}/revert/{versionId}")]
        //[RequirePermission(UserPermissions.UpdateVersions)]
        //[AuditLog("RevertToPreviousVersion")]
        //public async Task<ActionResult<ApiResponse<bool>>> RevertToPreviousVersion(
        //    string programId,
        //    string versionId,
        //    CancellationToken cancellationToken = default)
        //{
        //    var programObjectIdResult = ParseObjectId(programId, "programId");
        //    if (programObjectIdResult.Result != null) return programObjectIdResult.Result!;

        //    var versionObjectIdResult = ParseObjectId(versionId, "versionId");
        //    if (versionObjectIdResult.Result != null) return versionObjectIdResult.Result!;

        //    return await ExecuteAsync(async () =>
        //    {
        //        return await _versionService.RevertToPreviousVersionAsync(programId, versionId, cancellationToken);
        //    }, $"Revert program {programId} to version {versionId}");
        //}

        ///// <summary>
        ///// Set as current version
        ///// </summary>
        //[HttpPost("programs/{programId}/set-current/{versionId}")]
        //[RequirePermission(UserPermissions.UpdateVersions)]
        //[AuditLog("SetAsCurrentVersion")]
        //public async Task<ActionResult<ApiResponse<bool>>> SetAsCurrentVersion(
        //    string programId,
        //    string versionId,
        //    CancellationToken cancellationToken = default)
        //{
        //    var programObjectIdResult = ParseObjectId(programId, "programId");
        //    if (programObjectIdResult.Result != null) return programObjectIdResult.Result!;

        //    var versionObjectIdResult = ParseObjectId(versionId, "versionId");
        //    if (versionObjectIdResult.Result != null) return versionObjectIdResult.Result!;

        //    return await ExecuteAsync(async () =>
        //    {
        //        return await _versionService.SetAsCurrentVersionAsync(programId, versionId, cancellationToken);
        //    }, $"Set version {versionId} as current for program {programId}");
        //}

        //#endregion
    }
}