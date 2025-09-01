using MongoDB.Bson;
using TeiasMongoAPI.Services.DTOs.Permissions;

namespace TeiasMongoAPI.Services.Interfaces;

public interface IPermissionService
{
    // General Checks
    Task<bool> IsAdmin(ObjectId userId);
    Task<bool> IsDeveloper(ObjectId userId);

    // Program Permissions
    Task<bool> CanCreateProgram(ObjectId userId);
    Task<bool> CanViewProgramDetails(ObjectId userId, ObjectId programId);
    Task<bool> CanExecuteProgram(ObjectId userId, ObjectId programId);
    Task<bool> CanEditProgram(ObjectId userId, ObjectId programId); // Covers create, update, delete of program and its documents/files
    
    // Program Access Filtering
    Task<ProgramAccessResult> GetProgramAccessDetails(ObjectId userId);

    // TODO: We will add methods for Workflow and RemoteApp permissions later.
}