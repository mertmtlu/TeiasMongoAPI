using MongoDB.Bson;
using TeiasMongoAPI.Core.Enums;
using TeiasMongoAPI.Core.Models.Base;

namespace TeiasMongoAPI.Core.Models.Permissions;

public class UserWorkflowPermission : AEntityBase
{
    public ObjectId UserId { get; set; }
    public ObjectId WorkflowId { get; set; }
    public PermissionLevel Level { get; set; }
}