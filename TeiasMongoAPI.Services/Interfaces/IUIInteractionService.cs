using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.UIWorkflow;
using TeiasMongoAPI.Services.DTOs.Request.UIWorkflow;

namespace TeiasMongoAPI.Services.Interfaces
{
    public interface IUIInteractionService
    {
        Task<List<UIInteractionSession>> GetPendingUIInteractionsForUserAsync(string userId, CancellationToken cancellationToken = default);
        Task<List<UIInteractionSession>> GetUIInteractionsForWorkflowExecutionAsync(string workflowId, string executionId, CancellationToken cancellationToken = default);
        Task<UIInteractionSession?> GetUIInteractionAsync(string interactionId, CancellationToken cancellationToken = default);
        Task<bool> SubmitUIInteractionAsync(string interactionId, Dictionary<string, object> responseData, string userId, CancellationToken cancellationToken = default);
        Task<bool> CancelUIInteractionAsync(string interactionId, string userId, string? reason = null, CancellationToken cancellationToken = default);
        Task<UIInteractionSession> CreateUIInteractionAsync(string workflowId, string executionId, string nodeId, UIInteractionRequest request, string? uiComponentId = null, CancellationToken cancellationToken = default);
        Task<List<UIInteractionSession>> GetActiveInteractionsAsync(CancellationToken cancellationToken = default);
        Task ProcessTimedOutInteractionsAsync(CancellationToken cancellationToken = default);
    }
}