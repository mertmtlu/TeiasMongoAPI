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
    /// <summary>
    /// Version management controller - handles version lifecycle, review process, and deployment operations.
    /// File operations are handled separately through IFileStorageService in the service layer.
    /// </summary>
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

        #region Basic CRUD Operations

        /// <summary>
        /// Get all versions with pagination
        /// </summary>
        [HttpGet]
        [RequirePermission(UserPermissions.ViewVersions)]
        public async Task<ActionResult<ApiResponse<PagedResponse<VersionListDto>>>> GetAll(
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _versionService.GetAllAsync(pagination, cancellationToken);
            }, "Get all versions");
        }

        /// <summary>
        /// Get version by ID with full details including files
        /// </summary>
        [HttpGet("{id}")]
        [RequirePermission(UserPermissions.ViewVersions)]
        public async Task<ActionResult<ApiResponse<VersionDetailDto>>> GetById(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _versionService.GetByIdAsync(id, cancellationToken);
            }, $"Get version {id}");
        }

        /// <summary>
        /// Create new version for a program
        /// Note: This creates the version entity. Files are uploaded separately through the commit process.
        /// </summary>
        [HttpPost]
        [RequirePermission(UserPermissions.CreateVersions)]
        [AuditLog("CreateVersion")]
        public async Task<ActionResult<ApiResponse<VersionDto>>> Create(
            [FromBody] VersionCreateDto dto,
            CancellationToken cancellationToken = default)
        {
            var validationResult = ValidateModelState<VersionDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _versionService.CreateAsync(dto, CurrentUserId, cancellationToken);
            }, "Create version");
        }

        /// <summary>
        /// Update version metadata (commit message, review comments)
        /// Note: File changes should be done through the commit process
        /// </summary>
        [HttpPut("{id}")]
        [RequirePermission(UserPermissions.UpdateVersions)]
        [AuditLog("UpdateVersion")]
        public async Task<ActionResult<ApiResponse<VersionDto>>> Update(
            string id,
            [FromBody] VersionUpdateDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            var validationResult = ValidateModelState<VersionDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _versionService.UpdateAsync(id, dto, cancellationToken);
            }, $"Update version {id}");
        }

        /// <summary>
        /// Delete version (only if pending and not current)
        /// This will also delete associated files through IFileStorageService
        /// </summary>
        [HttpDelete("{id}")]
        [RequirePermission(UserPermissions.DeleteVersions)]
        [AuditLog("DeleteVersion")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _versionService.DeleteAsync(id, cancellationToken);
            }, $"Delete version {id}");
        }

        #endregion

        #region Version Discovery and Search

        /// <summary>
        /// Advanced version search with filtering
        /// </summary>
        [HttpPost("search")]
        [RequirePermission(UserPermissions.ViewVersions)]
        public async Task<ActionResult<ApiResponse<PagedResponse<VersionListDto>>>> Search(
            [FromBody] VersionSearchDto searchDto,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _versionService.SearchAsync(searchDto, pagination, cancellationToken);
            }, "Search versions");
        }

        /// <summary>
        /// Get all versions for a specific program
        /// </summary>
        [HttpGet("by-program/{programId}")]
        [RequirePermission(UserPermissions.ViewVersions)]
        public async Task<ActionResult<ApiResponse<PagedResponse<VersionListDto>>>> GetByProgram(
            string programId,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _versionService.GetByProgramIdAsync(programId, pagination, cancellationToken);
            }, $"Get versions by program {programId}");
        }

        /// <summary>
        /// Get latest version for a program
        /// </summary>
        [HttpGet("programs/{programId}/latest")]
        [RequirePermission(UserPermissions.ViewVersions)]
        public async Task<ActionResult<ApiResponse<VersionDto>>> GetLatestVersionForProgram(
            string programId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _versionService.GetLatestVersionForProgramAsync(programId, cancellationToken);
            }, $"Get latest version for program {programId}");
        }

        /// <summary>
        /// Get specific version by program and version number
        /// </summary>
        [HttpGet("programs/{programId}/version/{versionNumber}")]
        [RequirePermission(UserPermissions.ViewVersions)]
        public async Task<ActionResult<ApiResponse<VersionDto>>> GetByProgramAndVersionNumber(
            string programId,
            int versionNumber,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            if (versionNumber <= 0)
            {
                return ValidationError<VersionDto>("Version number must be greater than 0");
            }

            return await ExecuteAsync(async () =>
            {
                return await _versionService.GetByProgramIdAndVersionNumberAsync(programId, versionNumber, cancellationToken);
            }, $"Get version {versionNumber} for program {programId}");
        }

        /// <summary>
        /// Get next version number for a program
        /// </summary>
        [HttpGet("programs/{programId}/next-version-number")]
        [RequirePermission(UserPermissions.ViewVersions)]
        public async Task<ActionResult<ApiResponse<int>>> GetNextVersionNumber(
            string programId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _versionService.GetNextVersionNumberAsync(programId, cancellationToken);
            }, $"Get next version number for program {programId}");
        }

        /// <summary>
        /// Get versions by creator
        /// </summary>
        [HttpGet("by-creator/{creatorId}")]
        [RequirePermission(UserPermissions.ViewVersions)]
        public async Task<ActionResult<ApiResponse<PagedResponse<VersionListDto>>>> GetByCreator(
            string creatorId,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(creatorId, "creatorId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _versionService.GetByCreatorAsync(creatorId, pagination, cancellationToken);
            }, $"Get versions by creator {creatorId}");
        }

        /// <summary>
        /// Get versions by status (pending, approved, rejected)
        /// </summary>
        [HttpGet("by-status/{status}")]
        [RequirePermission(UserPermissions.ViewVersions)]
        public async Task<ActionResult<ApiResponse<PagedResponse<VersionListDto>>>> GetByStatus(
            string status,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _versionService.GetByStatusAsync(status, pagination, cancellationToken);
            }, $"Get versions by status {status}");
        }

        #endregion

        #region Version Review Process

        /// <summary>
        /// Get all versions pending review
        /// </summary>
        [HttpGet("pending-reviews")]
        [RequirePermission(UserPermissions.ViewVersions)]
        public async Task<ActionResult<ApiResponse<PagedResponse<VersionListDto>>>> GetPendingReviews(
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _versionService.GetPendingReviewsAsync(pagination, cancellationToken);
            }, "Get pending version reviews");
        }

        /// <summary>
        /// Get versions by reviewer
        /// </summary>
        [HttpGet("by-reviewer/{reviewerId}")]
        [RequirePermission(UserPermissions.ViewVersions)]
        public async Task<ActionResult<ApiResponse<PagedResponse<VersionListDto>>>> GetByReviewer(
            string reviewerId,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(reviewerId, "reviewerId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _versionService.GetByReviewerAsync(reviewerId, pagination, cancellationToken);
            }, $"Get versions by reviewer {reviewerId}");
        }

        /// <summary>
        /// Update version status
        /// </summary>
        [HttpPut("{id}/status")]
        [RequirePermission(UserPermissions.UpdateVersions)]
        [AuditLog("UpdateVersionStatus")]
        public async Task<ActionResult<ApiResponse<bool>>> UpdateStatus(
            string id,
            [FromBody] VersionStatusUpdateDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            var validationResult = ValidateModelState<bool>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _versionService.UpdateStatusAsync(id, dto, cancellationToken);
            }, $"Update status for version {id}");
        }

        /// <summary>
        /// Submit version review (approve or reject)
        /// </summary>
        [HttpPost("{id}/review")]
        [RequirePermission(UserPermissions.CreateVersions)]
        [AuditLog("SubmitVersionReview")]
        public async Task<ActionResult<ApiResponse<VersionReviewDto>>> SubmitReview(
            string id,
            [FromBody] VersionReviewSubmissionDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            var validationResult = ValidateModelState<VersionReviewDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _versionService.SubmitReviewAsync(id, dto, cancellationToken);
            }, $"Submit review for version {id}");
        }

        /// <summary>
        /// Quick approve version
        /// </summary>
        [HttpPost("{id}/approve")]
        [RequirePermission(UserPermissions.ApproveVersions)]
        [AuditLog("ApproveVersion")]
        public async Task<ActionResult<ApiResponse<VersionReviewDto>>> ApproveVersion(
            string id,
            [FromBody] string comments,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            if (string.IsNullOrWhiteSpace(comments))
            {
                return ValidationError<VersionReviewDto>("Comments are required for approval");
            }

            return await ExecuteAsync(async () =>
            {
                var reviewDto = new VersionReviewSubmissionDto
                {
                    Status = "approved",
                    Comments = comments
                };
                return await _versionService.SubmitReviewAsync(id, reviewDto, cancellationToken);
            }, $"Approve version {id}");
        }

        /// <summary>
        /// Quick reject version
        /// </summary>
        [HttpPost("{id}/reject")]
        [RequirePermission(UserPermissions.RejectVersions)]
        [AuditLog("RejectVersion")]
        public async Task<ActionResult<ApiResponse<VersionReviewDto>>> RejectVersion(
            string id,
            [FromBody] string comments,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            if (string.IsNullOrWhiteSpace(comments))
            {
                return ValidationError<VersionReviewDto>("Comments are required for rejection");
            }

            return await ExecuteAsync(async () =>
            {
                var reviewDto = new VersionReviewSubmissionDto
                {
                    Status = "rejected",
                    Comments = comments
                };
                return await _versionService.SubmitReviewAsync(id, reviewDto, cancellationToken);
            }, $"Reject version {id}");
        }

        #endregion

        #region Version Commit Operations

        /// <summary>
        /// Commit changes to create a new version with files
        /// This uses IFileStorageService internally to handle file operations
        /// Process: Upload files -> Commit -> Review -> Approve -> Execute
        /// </summary>
        [HttpPost("programs/{programId}/commit")]
        [RequirePermission(UserPermissions.CreateVersions)]
        [AuditLog("CommitVersionChanges")]
        public async Task<ActionResult<ApiResponse<VersionDto>>> CommitChanges(
            string programId,
            [FromBody] VersionCommitDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            var validationResult = ValidateModelState<VersionDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _versionService.CommitChangesAsync(programId, CurrentUserId, dto, cancellationToken);
            }, $"Commit changes for program {programId}");
        }

        /// <summary>
        /// Validate commit before actual commit
        /// This checks file validity and other constraints
        /// </summary>
        [HttpPost("programs/{programId}/validate-commit")]
        [RequirePermission(UserPermissions.ViewVersions)]
        public async Task<ActionResult<ApiResponse<bool>>> ValidateCommit(
            string programId,
            [FromBody] VersionCommitValidationDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            var validationResult = ValidateModelState<bool>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _versionService.ValidateCommitAsync(programId, dto, cancellationToken);
            }, $"Validate commit for program {programId}");
        }

        #endregion

        #region Version Comparison and Analysis

        /// <summary>
        /// Compare two versions and get differences
        /// </summary>
        [HttpGet("{fromVersionId}/compare/{toVersionId}")]
        [RequirePermission(UserPermissions.ViewVersions)]
        public async Task<ActionResult<ApiResponse<VersionDiffDto>>> CompareVersions(
            string fromVersionId,
            string toVersionId,
            CancellationToken cancellationToken = default)
        {
            var fromObjectIdResult = ParseObjectId(fromVersionId, "fromVersionId");
            if (fromObjectIdResult.Result != null) return fromObjectIdResult.Result!;

            var toObjectIdResult = ParseObjectId(toVersionId, "toVersionId");
            if (toObjectIdResult.Result != null) return toObjectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _versionService.GetDiffBetweenVersionsAsync(fromVersionId, toVersionId, cancellationToken);
            }, $"Compare versions {fromVersionId} and {toVersionId}");
        }

        /// <summary>
        /// Get diff from previous version
        /// </summary>
        [HttpGet("{versionId}/diff-from-previous")]
        [RequirePermission(UserPermissions.ViewVersions)]
        public async Task<ActionResult<ApiResponse<VersionDiffDto>>> GetDiffFromPrevious(
            string versionId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(versionId);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _versionService.GetDiffFromPreviousAsync(versionId, cancellationToken);
            }, $"Get diff from previous version for {versionId}");
        }

        /// <summary>
        /// Get change summary for a version
        /// </summary>
        [HttpGet("{versionId}/changes")]
        [RequirePermission(UserPermissions.ViewVersions)]
        public async Task<ActionResult<ApiResponse<List<VersionChangeDto>>>> GetChangeSummary(
            string versionId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(versionId);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _versionService.GetChangeSummaryAsync(versionId, cancellationToken);
            }, $"Get change summary for version {versionId}");
        }

        #endregion

        #region Version Deployment Operations

        /// <summary>
        /// Deploy approved version
        /// </summary>
        [HttpPost("{versionId}/deploy")]
        [RequirePermission(UserPermissions.DeployVersions)]
        [AuditLog("DeployVersion")]
        public async Task<ActionResult<ApiResponse<VersionDeploymentDto>>> DeployVersion(
            string versionId,
            [FromBody] VersionDeploymentRequestDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(versionId);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            var validationResult = ValidateModelState<VersionDeploymentDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _versionService.DeployVersionAsync(versionId, dto, cancellationToken);
            }, $"Deploy version {versionId}");
        }

        /// <summary>
        /// Revert program to previous version
        /// </summary>
        [HttpPost("programs/{programId}/revert/{versionId}")]
        [RequirePermission(UserPermissions.UpdateVersions)]
        [AuditLog("RevertToPreviousVersion")]
        public async Task<ActionResult<ApiResponse<bool>>> RevertToPreviousVersion(
            string programId,
            string versionId,
            CancellationToken cancellationToken = default)
        {
            var programObjectIdResult = ParseObjectId(programId, "programId");
            if (programObjectIdResult.Result != null) return programObjectIdResult.Result!;

            var versionObjectIdResult = ParseObjectId(versionId, "versionId");
            if (versionObjectIdResult.Result != null) return versionObjectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _versionService.RevertToPreviousVersionAsync(programId, versionId, cancellationToken);
            }, $"Revert program {programId} to version {versionId}");
        }

        /// <summary>
        /// Set version as current for program
        /// </summary>
        [HttpPost("programs/{programId}/set-current/{versionId}")]
        [RequirePermission(UserPermissions.UpdateVersions)]
        [AuditLog("SetAsCurrentVersion")]
        public async Task<ActionResult<ApiResponse<bool>>> SetAsCurrentVersion(
            string programId,
            string versionId,
            CancellationToken cancellationToken = default)
        {
            var programObjectIdResult = ParseObjectId(programId, "programId");
            if (programObjectIdResult.Result != null) return programObjectIdResult.Result!;

            var versionObjectIdResult = ParseObjectId(versionId, "versionId");
            if (versionObjectIdResult.Result != null) return versionObjectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _versionService.SetAsCurrentVersionAsync(programId, versionId, cancellationToken);
            }, $"Set version {versionId} as current for program {programId}");
        }

        #endregion

        #region Statistics and Analytics

        /// <summary>
        /// Get version statistics for a program
        /// </summary>
        [HttpGet("programs/{programId}/stats")]
        [RequirePermission(UserPermissions.ViewVersions)]
        public async Task<ActionResult<ApiResponse<VersionStatsDto>>> GetVersionStats(
            string programId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _versionService.GetVersionStatsAsync(programId, cancellationToken);
            }, $"Get version statistics for program {programId}");
        }

        /// <summary>
        /// Get version activity for a program
        /// </summary>
        [HttpGet("programs/{programId}/activity")]
        [RequirePermission(UserPermissions.ViewVersions)]
        public async Task<ActionResult<ApiResponse<List<VersionActivityDto>>>> GetVersionActivity(
            string programId,
            [FromQuery] int days = 30,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            if (days <= 0)
            {
                return ValidationError<List<VersionActivityDto>>("Days must be greater than 0");
            }

            return await ExecuteAsync(async () =>
            {
                return await _versionService.GetVersionActivityAsync(programId, days, cancellationToken);
            }, $"Get version activity for program {programId}");
        }

        #endregion
    }
}