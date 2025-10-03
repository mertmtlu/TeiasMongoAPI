using Microsoft.AspNetCore.Http;
using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.Collaboration;

namespace TeiasMongoAPI.Services.Interfaces
{
    public interface IDemoShowcaseService
    {
        // Public endpoints - New nested structure
        Task<PublicDemoShowcaseResponse> GetPublicDemoShowcaseAsync(CancellationToken cancellationToken = default);
        Task<UiComponentResponseDto> GetPublicUiComponentAsync(string appId, CancellationToken cancellationToken = default);
        Task<ExecutionResponseDto> ExecutePublicAppAsync(string appId, ExecutionRequestDto request, CancellationToken cancellationToken = default);

        // Public endpoint - Legacy
        Task<List<DemoShowcasePublicDto>> GetAllPublicAsync(CancellationToken cancellationToken = default);

        // Admin endpoints
        Task<List<DemoShowcaseDto>> GetAllAdminAsync(CancellationToken cancellationToken = default);
        Task<DemoShowcaseDto> CreateAsync(DemoShowcaseCreateDto dto, CancellationToken cancellationToken = default);
        Task<DemoShowcaseDto> UpdateAsync(string id, DemoShowcaseUpdateDto dto, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);

        // File upload
        Task<VideoUploadResponseDto> UploadVideoAsync(IFormFile file, CancellationToken cancellationToken = default);

        // Available apps
        Task<AvailableAppsDto> GetAvailableAppsAsync(CancellationToken cancellationToken = default);

        // Public execution monitoring
        Task<PublicExecutionDetailDto> GetPublicExecutionDetailAsync(string executionId, CancellationToken cancellationToken = default);
        Task<PublicExecutionLogsDto> GetPublicExecutionLogsAsync(string executionId, int lines = 100, CancellationToken cancellationToken = default);
        Task<PublicExecutionFilesDto> GetPublicExecutionFilesAsync(string executionId, CancellationToken cancellationToken = default);
        Task<VersionFileDetailDto> DownloadPublicExecutionFileAsync(string executionId, string filePath, CancellationToken cancellationToken = default);
    }
}
