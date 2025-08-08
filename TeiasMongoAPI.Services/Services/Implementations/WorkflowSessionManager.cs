using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TeiasMongoAPI.Core.Models.Collaboration;

namespace TeiasMongoAPI.Services.Services.Implementations
{
    public interface IWorkflowSessionManager
    {
        void AddSession(string executionId, WorkflowExecutionSession session);
        bool TryAddSessionIfNotRunning(string executionId, WorkflowExecutionSession session, string workflowId);
        WorkflowExecutionSession? GetSession(string executionId);
        bool TryGetSession(string executionId, out WorkflowExecutionSession? session);
        void RemoveSession(string executionId);
        bool IsWorkflowRunning(string workflowId);
        string? GetRunningExecutionId(string workflowId);
        int SessionCount { get; }
        IReadOnlyCollection<string> SessionKeys { get; }
    }

    public class WorkflowSessionManager : IWorkflowSessionManager, IDisposable
    {
        private readonly ConcurrentDictionary<string, WorkflowExecutionSession> _activeSessions = new();
        private readonly ILogger<WorkflowSessionManager> _logger;
        
        // ReaderWriterLockSlim for consistent protection of all operations
        private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);

        public WorkflowSessionManager(ILogger<WorkflowSessionManager> logger)
        {
            _logger = logger;
            _logger.LogInformation("WorkflowSessionManager singleton instance created: {InstanceHash}", GetHashCode());
        }

        // CORRECTED: Consistent locking for all write operations
        public void AddSession(string executionId, WorkflowExecutionSession session)
        {
            if (string.IsNullOrEmpty(executionId))
                throw new ArgumentException("ExecutionId cannot be null or empty", nameof(executionId));
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            _lock.EnterWriteLock();
            try
            {
                _activeSessions[executionId] = session;
                _logger.LogInformation("[SessionManager {InstanceHash}] Added session {ExecutionId}. Total sessions: {SessionCount}", 
                    GetHashCode(), executionId, _activeSessions.Count);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        // CORRECTED: Consistent locking for all read operations
        public WorkflowExecutionSession? GetSession(string executionId)
        {
            if (string.IsNullOrEmpty(executionId))
                return null;

            _lock.EnterReadLock();
            try
            {
                _activeSessions.TryGetValue(executionId, out var session);
                return session;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public bool TryGetSession(string executionId, out WorkflowExecutionSession? session)
        {
            session = null;
            if (string.IsNullOrEmpty(executionId))
                return false;

            _lock.EnterReadLock();
            try
            {
                var result = _activeSessions.TryGetValue(executionId, out session);
                if (!result)
                {
                    _logger.LogWarning("[SessionManager {InstanceHash}] Session {ExecutionId} not found. Current sessions: [{SessionKeys}]", 
                        GetHashCode(), executionId, string.Join(", ", _activeSessions.Keys));
                }
                return result;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        // CORRECTED: Consistent locking for all write operations
        public void RemoveSession(string executionId)
        {
            if (string.IsNullOrEmpty(executionId))
                return;

            _lock.EnterWriteLock();
            try
            {
                _activeSessions.TryRemove(executionId, out _);
                _logger.LogInformation("[SessionManager {InstanceHash}] Removed session {ExecutionId}. Remaining sessions: {SessionCount}", 
                    GetHashCode(), executionId, _activeSessions.Count);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool IsWorkflowRunning(string workflowId)
        {
            if (string.IsNullOrEmpty(workflowId))
                return false;

            _lock.EnterReadLock();
            try
            {
                return _activeSessions.Values.Any(session => 
                    session.Workflow._ID.ToString().Equals(workflowId, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public string? GetRunningExecutionId(string workflowId)
        {
            if (string.IsNullOrEmpty(workflowId))
                return null;

            _lock.EnterReadLock();
            try
            {
                var runningSession = _activeSessions.FirstOrDefault(kvp => 
                    kvp.Value.Workflow._ID.ToString().Equals(workflowId, StringComparison.OrdinalIgnoreCase));
                return runningSession.Key;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        // ATOMIC: Critical check-and-add operation under write lock
        public bool TryAddSessionIfNotRunning(string executionId, WorkflowExecutionSession session, string workflowId)
        {
            if (string.IsNullOrEmpty(executionId))
                throw new ArgumentException("ExecutionId cannot be null or empty", nameof(executionId));
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (string.IsNullOrEmpty(workflowId))
                throw new ArgumentException("WorkflowId cannot be null or empty", nameof(workflowId));

            _lock.EnterWriteLock();
            try
            {
                // Atomic check and add - both operations under same write lock
                var isAlreadyRunning = _activeSessions.Values.Any(s => 
                    s.Workflow._ID.ToString().Equals(workflowId, StringComparison.OrdinalIgnoreCase));

                if (isAlreadyRunning)
                {
                    _logger.LogWarning("[SessionManager {InstanceHash}] Cannot add session {ExecutionId} - workflow {WorkflowId} is already running", 
                        GetHashCode(), executionId, workflowId);
                    return false;
                }

                _activeSessions[executionId] = session;
                _logger.LogInformation("[SessionManager {InstanceHash}] Added session {ExecutionId} for workflow {WorkflowId}. Total sessions: {SessionCount}", 
                    GetHashCode(), executionId, workflowId, _activeSessions.Count);
                return true;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public int SessionCount 
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return _activeSessions.Count;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }

        public IReadOnlyCollection<string> SessionKeys 
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return _activeSessions.Keys.ToList();
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }

        public void Dispose()
        {
            _lock?.Dispose();
        }
    }
}