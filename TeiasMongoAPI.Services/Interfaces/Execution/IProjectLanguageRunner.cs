using TeiasMongoAPI.Services.DTOs.Request.Execution;
using TeiasMongoAPI.Services.DTOs.Response.Execution;

namespace TeiasMongoAPI.Services.Interfaces.Execution
{
    public interface IProjectLanguageRunner
    {
        string Language { get; }
        int Priority { get; }

        Task<bool> CanHandleProjectAsync(string projectDirectory, CancellationToken cancellationToken = default);
        Task<ProjectStructureAnalysis> AnalyzeProjectAsync(string projectDirectory, CancellationToken cancellationToken = default);
        Task<ProjectBuildResult> BuildProjectAsync(string projectDirectory, ProjectBuildArgs buildArgs, CancellationToken cancellationToken = default);
        Task<ProjectExecutionResult> ExecuteAsync(ProjectExecutionContext context, CancellationToken cancellationToken = default);
        Task<ProjectValidationResult> ValidateProjectAsync(string projectDirectory, CancellationToken cancellationToken = default);
    }
}