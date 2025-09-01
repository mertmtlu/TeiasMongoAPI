using MongoDB.Bson;
using TeiasMongoAPI.Core.Enums;
using TeiasMongoAPI.Core.Models.Base;

namespace TeiasMongoAPI.Core.Models.Permissions;

public class UserProgramPermission : AEntityBase
{
    public ObjectId UserId { get; set; }
    public ObjectId ProgramId { get; set; }
    public PermissionLevel Level { get; set; }
}