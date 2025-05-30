﻿using AutoMapper;
using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.Collaboration;
using Version = TeiasMongoAPI.Core.Models.Collaboration.Version;

namespace TeiasMongoAPI.Services.Mappings
{
    public class VersionMappingProfile : Profile
    {
        public VersionMappingProfile()
        {
            // Request to Domain
            CreateMap<VersionCreateDto, Version>()
                .ForMember(dest => dest._ID, opt => opt.Ignore()) // Generated by MongoDB
                .ForMember(dest => dest.ProgramId, opt => opt.MapFrom(src => ObjectId.Parse(src.ProgramId)))
                .ForMember(dest => dest.VersionNumber, opt => opt.Ignore()) // Calculated in service
                .ForMember(dest => dest.CreatedBy, opt => opt.Ignore()) // Set from current user in service
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => "pending"))
                .ForMember(dest => dest.Reviewer, opt => opt.Ignore())
                .ForMember(dest => dest.ReviewedAt, opt => opt.Ignore())
                .ForMember(dest => dest.ReviewComments, opt => opt.Ignore())
                .ForMember(dest => dest.Files, opt => opt.MapFrom(src => src.Files));

            CreateMap<VersionUpdateDto, Version>()
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

            CreateMap<VersionFileCreateDto, VersionFile>()
                .ForMember(dest => dest.StorageKey, opt => opt.Ignore()) // Generated in service
                .ForMember(dest => dest.Hash, opt => opt.Ignore()) // Calculated in service
                .ForMember(dest => dest.Size, opt => opt.MapFrom(src => src.Content.Length));

            CreateMap<VersionCommitDto, Version>()
                .ForMember(dest => dest._ID, opt => opt.Ignore())
                .ForMember(dest => dest.ProgramId, opt => opt.Ignore())
                .ForMember(dest => dest.VersionNumber, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => "pending"))
                .ForMember(dest => dest.Files, opt => opt.Ignore()) // Mapped from Changes in service
                .ForMember(dest => dest.Reviewer, opt => opt.Ignore())
                .ForMember(dest => dest.ReviewedAt, opt => opt.Ignore())
                .ForMember(dest => dest.ReviewComments, opt => opt.Ignore());

            // Domain to Response
            CreateMap<Version, VersionDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.ProgramId, opt => opt.MapFrom(src => src.ProgramId.ToString()));

            CreateMap<Version, VersionListDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.ProgramId, opt => opt.MapFrom(src => src.ProgramId.ToString()))
                .ForMember(dest => dest.ProgramName, opt => opt.Ignore()) // Resolved in service
                .ForMember(dest => dest.CreatedByName, opt => opt.Ignore()) // Resolved in service
                .ForMember(dest => dest.ReviewerName, opt => opt.Ignore()) // Resolved in service
                .ForMember(dest => dest.FileCount, opt => opt.MapFrom(src => src.Files.Count))
                .ForMember(dest => dest.IsCurrent, opt => opt.Ignore()); // Determined in service

            CreateMap<Version, VersionDetailDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.ProgramId, opt => opt.MapFrom(src => src.ProgramId.ToString()))
                .ForMember(dest => dest.ProgramName, opt => opt.Ignore()) // Resolved in service
                .ForMember(dest => dest.CreatedByName, opt => opt.Ignore()) // Resolved in service
                .ForMember(dest => dest.ReviewerName, opt => opt.Ignore()) // Resolved in service
                .ForMember(dest => dest.Files, opt => opt.MapFrom(src => src.Files))
                .ForMember(dest => dest.Stats, opt => opt.Ignore()) // Calculated in service
                .ForMember(dest => dest.DeploymentInfo, opt => opt.Ignore()); // Resolved in service

            CreateMap<VersionFile, VersionFileDto>()
                .ForMember(dest => dest.ContentType, opt => opt.MapFrom(src => GetContentTypeFromExtension(src.Path)));

            CreateMap<VersionFile, VersionFileDetailDto>()
                .ForMember(dest => dest.ContentType, opt => opt.MapFrom(src => GetContentTypeFromExtension(src.Path)))
                .ForMember(dest => dest.Content, opt => opt.Ignore()) // Loaded from storage in service
                .ForMember(dest => dest.LastModified, opt => opt.Ignore()); // From storage metadata

            // Review mappings
            CreateMap<VersionStatusUpdateDto, Version>()
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status))
                .ForMember(dest => dest.ReviewComments, opt => opt.MapFrom(src => src.Comments))
                .ForMember(dest => dest.Reviewer, opt => opt.Ignore()) // Set from current user in service
                .ForMember(dest => dest.ReviewedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest._ID, opt => opt.Ignore())
                .ForMember(dest => dest.ProgramId, opt => opt.Ignore())
                .ForMember(dest => dest.VersionNumber, opt => opt.Ignore())
                .ForMember(dest => dest.CommitMessage, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.Files, opt => opt.Ignore());

            CreateMap<VersionReviewSubmissionDto, Version>()
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status))
                .ForMember(dest => dest.ReviewComments, opt => opt.MapFrom(src => src.Comments))
                .ForMember(dest => dest.Reviewer, opt => opt.Ignore()) // Set from current user in service
                .ForMember(dest => dest.ReviewedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest._ID, opt => opt.Ignore())
                .ForMember(dest => dest.ProgramId, opt => opt.Ignore())
                .ForMember(dest => dest.VersionNumber, opt => opt.Ignore())
                .ForMember(dest => dest.CommitMessage, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.Files, opt => opt.Ignore());

            CreateMap<Version, VersionReviewDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.VersionId, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status))
                .ForMember(dest => dest.Comments, opt => opt.MapFrom(src => src.ReviewComments ?? string.Empty))
                .ForMember(dest => dest.ReviewedBy, opt => opt.MapFrom(src => src.Reviewer ?? string.Empty))
                .ForMember(dest => dest.ReviewedByName, opt => opt.Ignore()) // Resolved in service
                .ForMember(dest => dest.ReviewedAt, opt => opt.MapFrom(src => src.ReviewedAt ?? DateTime.MinValue));

            // Statistics and comparison mappings
            CreateMap<List<VersionFile>, VersionStatsDto>()
                .ConvertUsing(src => new VersionStatsDto
                {
                    TotalFiles = src.Count,
                    TotalSize = src.Sum(f => f.Size),
                    FileTypeCount = src.GroupBy(f => GetFileExtension(f.Path))
                                      .ToDictionary(g => g.Key, g => g.Count()),
                    ExecutionCount = 0, // This would be populated from execution service
                    IsCurrentVersion = false // This would be determined in service
                });

            // File change mappings
            CreateMap<VersionFileChangeDto, VersionFile>()
                .ForMember(dest => dest.Path, opt => opt.MapFrom(src => src.Path))
                .ForMember(dest => dest.StorageKey, opt => opt.Ignore()) // Generated in service
                .ForMember(dest => dest.Hash, opt => opt.Ignore()) // Calculated in service
                .ForMember(dest => dest.Size, opt => opt.MapFrom(src => src.Content != null ? src.Content.Length : 0))
                .ForMember(dest => dest.FileType, opt => opt.MapFrom(src => GetFileTypeFromPath(src.Path)));

            // Deployment mappings
            CreateMap<VersionDeploymentRequestDto, object>()
                .ConvertUsing(src => new
                {
                    Configuration = src.DeploymentConfiguration,
                    Environments = src.TargetEnvironments,
                    SetAsCurrent = src.SetAsCurrent,
                    DeployedAt = DateTime.UtcNow
                });
        }

        private string GetContentTypeFromExtension(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".cs" => "text/x-csharp",
                ".js" => "application/javascript",
                ".ts" => "application/typescript",
                ".py" => "text/x-python",
                ".html" => "text/html",
                ".css" => "text/css",
                ".json" => "application/json",
                ".xml" => "application/xml",
                ".txt" => "text/plain",
                ".md" => "text/markdown",
                ".yml" or ".yaml" => "application/x-yaml",
                ".sql" => "application/sql",
                ".dockerfile" => "text/x-dockerfile",
                _ => "application/octet-stream"
            };
        }

        private string GetFileExtension(string filePath)
        {
            var extension = Path.GetExtension(filePath);
            return string.IsNullOrEmpty(extension) ? "no-extension" : extension.TrimStart('.');
        }

        private string GetFileTypeFromPath(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".cs" or ".js" or ".ts" or ".py" or ".cpp" or ".c" or ".h" or ".java" => "source",
                ".html" or ".css" or ".scss" or ".less" => "web",
                ".json" or ".xml" or ".yml" or ".yaml" or ".config" => "config",
                ".png" or ".jpg" or ".jpeg" or ".gif" or ".svg" or ".ico" => "asset",
                ".dll" or ".exe" or ".so" or ".dylib" => "build_artifact",
                ".md" or ".txt" or ".rst" => "documentation",
                _ => "other"
            };
        }
    }
}