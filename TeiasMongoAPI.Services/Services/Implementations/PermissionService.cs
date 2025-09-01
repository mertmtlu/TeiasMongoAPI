using MongoDB.Bson;
using TeiasMongoAPI.Core.Enums;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Core.Models.Permissions;
using TeiasMongoAPI.Services.DTOs.Permissions;
using TeiasMongoAPI.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace TeiasMongoAPI.Services.Services.Implementations;

public class PermissionService : IPermissionService
{
    private readonly IGenericRepository<UserProgramPermission> _userProgramPermissionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PermissionService> _logger;

    public PermissionService(
        IGenericRepository<UserProgramPermission> userProgramPermissionRepository,
        IUnitOfWork unitOfWork,
        ILogger<PermissionService> logger)
    {
        _userProgramPermissionRepository = userProgramPermissionRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<bool> IsAdmin(ObjectId userId)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found", userId);
            return false;
        }

        return user.Role == UserRole.Admin;
    }

    public async Task<bool> IsDeveloper(ObjectId userId)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found", userId);
            return false;
        }

        return user.Role == UserRole.Admin ||
               user.Role == UserRole.InternalDeveloper ||
               user.Role == UserRole.ExternalDeveloper;
    }

    public async Task<bool> CanCreateProgram(ObjectId userId)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found", userId);
            return false;
        }

        // Admins, Internal Developers, and External Developers can create programs
        var canCreate = user.Role == UserRole.Admin ||
                       user.Role == UserRole.InternalDeveloper ||
                       user.Role == UserRole.ExternalDeveloper;

        _logger.LogDebug("User {UserId} with role {Role} {Action} create programs", 
            userId, user.Role, canCreate ? "can" : "cannot");
        
        return canCreate;
    }

    public async Task<bool> CanViewProgramDetails(ObjectId userId, ObjectId programId)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found", userId);
            return false;
        }

        // Admins, Internal Developers, and Internal Users can view all programs
        if (user.Role == UserRole.Admin || 
            user.Role == UserRole.InternalDeveloper || 
            user.Role == UserRole.InternalUser)
        {
            _logger.LogDebug("User {UserId} with role {Role} can view program {ProgramId} - role-based access", 
                userId, user.Role, programId);
            return true;
        }

        // For External Developers and External Users, check for specific permission
        var permission = await _userProgramPermissionRepository.FindOneAsync(
            p => p.UserId == userId && p.ProgramId == programId);

        var hasPermission = permission != null;
        _logger.LogDebug("User {UserId} with role {Role} {Action} view program {ProgramId} - permission-based access", 
            userId, user.Role, hasPermission ? "can" : "cannot", programId);
        
        return hasPermission;
    }

    public async Task<bool> CanExecuteProgram(ObjectId userId, ObjectId programId)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found", userId);
            return false;
        }

        // Admins and Internal Developers can execute all programs
        if (user.Role == UserRole.Admin || user.Role == UserRole.InternalDeveloper)
        {
            _logger.LogDebug("User {UserId} with role {Role} can execute program {ProgramId} - role-based access", 
                userId, user.Role, programId);
            return true;
        }

        // For all other roles, check for specific permission
        var permission = await _userProgramPermissionRepository.FindOneAsync(
            p => p.UserId == userId && p.ProgramId == programId);

        var hasPermission = permission != null;
        _logger.LogDebug("User {UserId} with role {Role} {Action} execute program {ProgramId} - permission-based access", 
            userId, user.Role, hasPermission ? "can" : "cannot", programId);
        
        return hasPermission;
    }

    public async Task<bool> CanEditProgram(ObjectId userId, ObjectId programId)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found", userId);
            return false;
        }

        // Admins and Internal Developers can edit all programs
        if (user.Role == UserRole.Admin || user.Role == UserRole.InternalDeveloper)
        {
            _logger.LogDebug("User {UserId} with role {Role} can edit program {ProgramId} - role-based access", 
                userId, user.Role, programId);
            return true;
        }

        // External Developers need ReadWrite permission level
        if (user.Role == UserRole.ExternalDeveloper)
        {
            var permission = await _userProgramPermissionRepository.FindOneAsync(
                p => p.UserId == userId && p.ProgramId == programId && p.Level == PermissionLevel.ReadWrite);

            var hasPermission = permission != null;
            _logger.LogDebug("User {UserId} with role {Role} {Action} edit program {ProgramId} - ReadWrite permission required", 
                userId, user.Role, hasPermission ? "can" : "cannot", programId);
            
            return hasPermission;
        }

        // All other roles (InternalUser, ExternalUser) cannot edit programs
        _logger.LogDebug("User {UserId} with role {Role} cannot edit program {ProgramId} - role restriction", 
            userId, user.Role, programId);
        return false;
    }

    public async Task<ProgramAccessResult> GetProgramAccessDetails(ObjectId userId)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found", userId);
            return new ProgramAccessResult { AccessType = ProgramAccessType.None };
        }

        // Admin, Internal Developers, and Internal Users can see all programs
        if (user.Role == UserRole.Admin ||
            user.Role == UserRole.InternalDeveloper ||
            user.Role == UserRole.InternalUser)
        {
            _logger.LogDebug("User {UserId} with role {Role} has access to all programs", userId, user.Role);
            return new ProgramAccessResult { AccessType = ProgramAccessType.All };
        }

        // External Developers and External Users can only see specific programs
        if (user.Role == UserRole.ExternalDeveloper || user.Role == UserRole.ExternalUser)
        {
            var permissions = await _userProgramPermissionRepository.FindAsync(p => p.UserId == userId);
            var allowedProgramIds = permissions.Select(p => p.ProgramId).ToList();

            _logger.LogDebug("User {UserId} with role {Role} has access to {Count} specific programs", 
                userId, user.Role, allowedProgramIds.Count);

            return new ProgramAccessResult
            {
                AccessType = ProgramAccessType.Specific,
                AllowedProgramIds = allowedProgramIds
            };
        }

        // Fallback - should not happen with current enum values
        _logger.LogWarning("User {UserId} has unknown role {Role}, denying access", userId, user.Role);
        return new ProgramAccessResult { AccessType = ProgramAccessType.None };
    }
}