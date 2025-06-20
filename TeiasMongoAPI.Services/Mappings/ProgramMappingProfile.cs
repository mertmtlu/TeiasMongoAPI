﻿using AutoMapper;
using System.Text.Json;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.Collaboration;

namespace TeiasMongoAPI.Services.Mappings
{
    public class ProgramMappingProfile : Profile
    {
        public ProgramMappingProfile()
        {
            // Request to Domain
            CreateMap<ProgramCreateDto, Program>()
                .ForMember(dest => dest._ID, opt => opt.Ignore()) // Will be generated by MongoDB
                .ForMember(dest => dest.Creator, opt => opt.Ignore()) // Will be set in service from current user
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => "draft"))
                .ForMember(dest => dest.CurrentVersion, opt => opt.Ignore()) // Set when first version is created
                .ForMember(dest => dest.Permissions, opt => opt.MapFrom(src => new ProgramPermissions()))
                .ForMember(dest => dest.UiConfiguration, opt => opt.MapFrom(src => ConvertJsonElement(src.UiConfiguration)))
                .ForMember(dest => dest.Metadata, opt => opt.MapFrom(src => ConvertJsonElement(src.Metadata)));

            CreateMap<ProgramUpdateDto, Program>()
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

            // Domain to Response
            CreateMap<Program, ProgramDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()));

            CreateMap<Program, ProgramListDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.DeploymentType, opt => opt.MapFrom(src => src.DeploymentInfo != null ? src.DeploymentInfo.DeploymentType : (AppDeploymentType?)null))
                .ForMember(dest => dest.DeploymentStatus, opt => opt.MapFrom(src => src.DeploymentInfo != null ? src.DeploymentInfo.Status : null));

            CreateMap<Program, ProgramDetailDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.Permissions, opt => opt.MapFrom(src => MapProgramPermissions(src.Permissions)))
                .ForMember(dest => dest.Files, opt => opt.Ignore()) // Will be populated from file service
                .ForMember(dest => dest.DeploymentStatus, opt => opt.Ignore()) // Will be populated from deployment service
                .ForMember(dest => dest.Stats, opt => opt.Ignore()); // Will be populated from statistics service

            // Permission mappings
            CreateMap<GroupPermission, ProgramPermissionDto>()
                .ForMember(dest => dest.Type, opt => opt.MapFrom(src => "group"))
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.GroupId))
                .ForMember(dest => dest.Name, opt => opt.Ignore()) // Will be resolved in service
                .ForMember(dest => dest.AccessLevel, opt => opt.MapFrom(src => src.AccessLevel));

            CreateMap<UserPermission, ProgramPermissionDto>()
                .ForMember(dest => dest.Type, opt => opt.MapFrom(src => "user"))
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.UserId))
                .ForMember(dest => dest.Name, opt => opt.Ignore()) // Will be resolved in service
                .ForMember(dest => dest.AccessLevel, opt => opt.MapFrom(src => src.AccessLevel));

            // File mappings
            CreateMap<ProgramFileUploadDto, ProgramFileDto>()
                .ForMember(dest => dest.Path, opt => opt.MapFrom(src => src.Path))
                .ForMember(dest => dest.ContentType, opt => opt.MapFrom(src => src.ContentType))
                .ForMember(dest => dest.Size, opt => opt.MapFrom(src => src.Content.Length))
                .ForMember(dest => dest.LastModified, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.Hash, opt => opt.Ignore()) // Will be calculated in service
                .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description));

            // Deployment mappings
            CreateMap<AppDeploymentInfo, ProgramDeploymentDto>()
                .ForMember(dest => dest.Id, opt => opt.Ignore()) // Generated in service
                .ForMember(dest => dest.ApplicationUrl, opt => opt.Ignore()) // Set based on deployment
                .ForMember(dest => dest.Logs, opt => opt.Ignore()); // Populated from deployment service
        }

        private List<ProgramPermissionDto> MapProgramPermissions(ProgramPermissions permissions)
        {
            var result = new List<ProgramPermissionDto>();

            // Map group permissions
            if (permissions.Groups != null)
            {
                foreach (var group in permissions.Groups)
                {
                    result.Add(new ProgramPermissionDto
                    {
                        Type = "group",
                        Id = group.GroupId,
                        AccessLevel = group.AccessLevel,
                        Name = string.Empty // Will be resolved in service
                    });
                }
            }

            // Map user permissions
            if (permissions.Users != null)
            {
                foreach (var user in permissions.Users)
                {
                    result.Add(new ProgramPermissionDto
                    {
                        Type = "user",
                        Id = user.UserId,
                        AccessLevel = user.AccessLevel,
                        Name = string.Empty // Will be resolved in service
                    });
                }
            }

            return result;
        }

        private static object ConvertJsonElement(object value)
        {
            if (value is JsonElement jsonElement)
            {
                return ConvertJsonElementToObject(jsonElement);
            }
            return value ?? new object();
        }

        private static object ConvertJsonElementToObject(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Object => element.EnumerateObject()
                    .ToDictionary(
                        prop => prop.Name,
                        prop => ConvertJsonElementToObject(prop.Value)
                    ),
                JsonValueKind.Array => element.EnumerateArray()
                    .Select(ConvertJsonElementToObject)
                    .ToArray(),
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number when element.TryGetInt32(out int intValue) => intValue,
                JsonValueKind.Number when element.TryGetInt64(out long longValue) => longValue,
                JsonValueKind.Number when element.TryGetDouble(out double doubleValue) => doubleValue,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => new object()
            };
        }
    }
}