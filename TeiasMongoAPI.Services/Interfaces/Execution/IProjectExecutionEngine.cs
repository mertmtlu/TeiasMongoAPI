using TeiasMongoAPI.Services.DTOs.Request.Execution;
using TeiasMongoAPI.Services.DTOs.Response.Execution;

namespace TeiasMongoAPI.Services.Interfaces.Execution
{
    public interface IProjectExecutionEngine
    {
        Task<ProjectExecutionResult> ExecuteProjectAsync(ProjectExecutionRequest request, string executionId, CancellationToken cancellationToken = default);
        Task<ProjectValidationResult> ValidateProjectAsync(string programId, string versionId, CancellationToken cancellationToken = default);
        Task<ProjectStructureAnalysis> AnalyzeProjectStructureAsync(string programId, string versionId, CancellationToken cancellationToken = default);
        Task<bool> CancelExecutionAsync(string executionId, CancellationToken cancellationToken = default);
        Task<List<string>> GetSupportedLanguagesAsync(CancellationToken cancellationToken = default);
    }
}