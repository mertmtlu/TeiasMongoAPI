namespace TeiasMongoAPI.Services.DTOs.Permissions;

public enum ProgramAccessType
{
    All,        // User can see all programs.
    Specific,   // User can only see a specific list of programs.
    None        // User has no access to any programs.
}