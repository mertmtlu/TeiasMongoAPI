using AutoMapper;
using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.Group;
using TeiasMongoAPI.Services.DTOs.Response.Group;

namespace TeiasMongoAPI.Services.Mappings
{
    public class GroupMappingProfile : Profile
    {
        public GroupMappingProfile()
        {
            // Request DTOs to Entity mappings
            CreateMap<GroupCreateDto, Group>()
                .ForMember(dest => dest._ID, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.ModifiedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => true))
                .ForMember(dest => dest.Members, opt => opt.MapFrom(src => ConvertStringIdsToObjectIds(src.MemberIds)));

            CreateMap<GroupUpdateDto, Group>()
                .ForMember(dest => dest._ID, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.ModifiedAt, opt => opt.Ignore())
                .ForMember(dest => dest.Members, opt => opt.Ignore())
                .ForAllMembers(opt => opt.Condition((src, dest, srcMember) => srcMember != null));

            // Entity to Response DTOs mappings
            CreateMap<Group, GroupDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.CreatedByName, opt => opt.Ignore()) // Will be populated by service
                .ForMember(dest => dest.MemberCount, opt => opt.MapFrom(src => src.Members.Count))
                .ForMember(dest => dest.Members, opt => opt.Ignore()); // Will be populated by service

            CreateMap<Group, GroupListDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.CreatedByName, opt => opt.Ignore()) // Will be populated by service
                .ForMember(dest => dest.MemberCount, opt => opt.MapFrom(src => src.Members.Count));

            // ObjectId to string and vice versa converters
            CreateMap<ObjectId, string>().ConvertUsing(id => id.ToString());
            CreateMap<string, ObjectId>().ConvertUsing<StringToObjectIdConverter>();
        }

        private static List<ObjectId> ConvertStringIdsToObjectIds(List<string> stringIds)
        {
            var objectIds = new List<ObjectId>();
            foreach (var stringId in stringIds)
            {
                if (ObjectId.TryParse(stringId, out var objectId))
                {
                    objectIds.Add(objectId);
                }
            }
            return objectIds;
        }
    }

    public class StringToObjectIdConverter : ITypeConverter<string, ObjectId>
    {
        public ObjectId Convert(string source, ObjectId destination, ResolutionContext context)
        {
            if (string.IsNullOrEmpty(source))
                return ObjectId.Empty;

            return ObjectId.TryParse(source, out var objectId) ? objectId : ObjectId.Empty;
        }
    }
}