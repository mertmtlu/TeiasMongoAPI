using MongoDB.Bson;

namespace TeiasMongoAPI.Services.DTOs.Permissions;

public class ProgramAccessResult
{
    public ProgramAccessType AccessType { get; set; }
    public List<ObjectId> AllowedProgramIds { get; set; } = new List<ObjectId>();
}