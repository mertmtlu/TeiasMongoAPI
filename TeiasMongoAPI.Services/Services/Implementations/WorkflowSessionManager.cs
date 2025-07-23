using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TeiasMongoAPI.Core.Models.Collaboration;

namespace TeiasMongoAPI.Services.Services.Implementations
{
    public interface IWorkflowSessionManager
    {
        void AddSession(string executionId, WorkflowExecutionSession session);
        WorkflowExecutionSession? GetSession(string executionId);
        bool TryGetSession(string executionId, out WorkflowExecutionSession? session);
        void RemoveSession(string executionId);
        int SessionCount { get; }
        IReadOnlyCollection<string> SessionKeys { get; }
    }

    public class WorkflowSessionManager : IWorkflowSessionManager
    {
        private readonly ConcurrentDictionary<string, WorkflowExecutionSession> _activeSessions = new();
        private readonly ILogger<WorkflowSessionManager> _logger;

        public WorkflowSessionManager(ILogger<WorkflowSessionManager> logger)
        {
            _logger = logger;
            _logger.LogInformation($"WorkflowSessionManager singleton instance created: {GetHashCode()}");
        }

        public void AddSession(string executionId, WorkflowExecutionSession session)
        {
            _activeSessions[executionId] = session;
            _logger.LogInformation($"[SessionManager {GetHashCode()}] Added session {executionId}. Total sessions: {_activeSessions.Count}");
        }

        public WorkflowExecutionSession? GetSession(string executionId)
        {
            _activeSessions.TryGetValue(executionId, out var session);
            return session;
        }

        public bool TryGetSession(string executionId, out WorkflowExecutionSession? session)
        {
            var result = _activeSessions.TryGetValue(executionId, out session);
            if (!result)
            {
                _logger.LogWarning($"[SessionManager {GetHashCode()}] Session {executionId} not found. Current sessions: [{string.Join(", ", _activeSessions.Keys)}]");
            }
            return result;
        }

        public void RemoveSession(string executionId)
        {
            _activeSessions.TryRemove(executionId, out _);
            _logger.LogInformation($"[SessionManager {GetHashCode()}] Removed session {executionId}. Remaining sessions: {_activeSessions.Count}");
        }

        public int SessionCount => _activeSessions.Count;

        public IReadOnlyCollection<string> SessionKeys => _activeSessions.Keys.ToList();
    }
}