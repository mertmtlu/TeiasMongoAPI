using MongoDB.Bson;
using TeiasMongoAPI.Core.Enums;
using TeiasMongoAPI.Core.Models.Base;

namespace TeiasMongoAPI.Core.Models.Permissions;

public class UserRemoteAppPermission : AEntityBase
{
    public ObjectId UserId { get; set; }
    public ObjectId RemoteAppId { get; set; }
    public PermissionLevel Level { get; set; }
}