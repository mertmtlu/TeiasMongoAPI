﻿using AutoMapper;
using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.Collaboration;

namespace TeiasMongoAPI.Services.Mappings
{
    public class ExecutionMappingProfile : Profile
    {
        public ExecutionMappingProfile()
        {
            // Request to Domain
            CreateMap<ProgramExecutionRequestDto, Execution>()
                .ForMember(dest => dest._ID, opt => opt.Ignore()) // Generated by MongoDB
                .ForMember(dest => dest.ProgramId, opt => opt.Ignore()) // Set in service
                .ForMember(dest => dest.VersionId, opt => opt.Ignore()) // Set in service
                .ForMember(dest => dest.UserId, opt => opt.Ignore()) // Set from current user in service
                .ForMember(dest => dest.ExecutionType, opt => opt.MapFrom(src => "code_execution"))
                .ForMember(dest => dest.StartedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.CompletedAt, opt => opt.Ignore()) // Set when execution completes
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => "running"))
                .ForMember(dest => dest.Parameters, opt => opt.MapFrom(src => src.Parameters))
                .ForMember(dest => dest.Results, opt => opt.MapFrom(src => new ExecutionResults()))
                .ForMember(dest => dest.ResourceUsage, opt => opt.MapFrom(src => new ResourceUsage()));

            CreateMap<VersionExecutionRequestDto, Execution>()
                .ForMember(dest => dest._ID, opt => opt.Ignore())
                .ForMember(dest => dest.ProgramId, opt => opt.Ignore())
                .ForMember(dest => dest.VersionId, opt => opt.Ignore())
                .ForMember(dest => dest.UserId, opt => opt.Ignore())
                .ForMember(dest => dest.ExecutionType, opt => opt.MapFrom(src => "version_execution"))
                .ForMember(dest => dest.StartedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.CompletedAt, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => "running"))
                .ForMember(dest => dest.Parameters, opt => opt.MapFrom(src => src.Parameters))
                .ForMember(dest => dest.Results, opt => opt.MapFrom(src => new ExecutionResults()))
                .ForMember(dest => dest.ResourceUsage, opt => opt.MapFrom(src => new ResourceUsage()));

            CreateMap<ExecutionParametersDto, Execution>()
                .ForMember(dest => dest._ID, opt => opt.Ignore())
                .ForMember(dest => dest.ProgramId, opt => opt.MapFrom(src => ObjectId.Parse(src.ProgramId)))
                .ForMember(dest => dest.VersionId, opt => opt.MapFrom(src => !string.IsNullOrEmpty(src.VersionId) ? ObjectId.Parse(src.VersionId) : ObjectId.Empty))
                .ForMember(dest => dest.UserId, opt => opt.Ignore())
                .ForMember(dest => dest.ExecutionType, opt => opt.MapFrom(src => "parameterized_execution"))
                .ForMember(dest => dest.StartedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.CompletedAt, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => "running"))
                .ForMember(dest => dest.Parameters, opt => opt.MapFrom(src => src.Parameters))
                .ForMember(dest => dest.Results, opt => opt.MapFrom(src => new ExecutionResults()))
                .ForMember(dest => dest.ResourceUsage, opt => opt.MapFrom(src => new ResourceUsage()));

            // Domain to Response
            CreateMap<Execution, ExecutionDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.ProgramId, opt => opt.MapFrom(src => src.ProgramId.ToString()))
                .ForMember(dest => dest.VersionId, opt => opt.MapFrom(src => src.VersionId.ToString()));

            CreateMap<Execution, ExecutionListDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.ProgramId, opt => opt.MapFrom(src => src.ProgramId.ToString()))
                .ForMember(dest => dest.VersionId, opt => opt.MapFrom(src => src.VersionId.ToString()))
                .ForMember(dest => dest.ProgramName, opt => opt.Ignore()) // Resolved in service
                .ForMember(dest => dest.VersionNumber, opt => opt.Ignore()) // Resolved in service
                .ForMember(dest => dest.UserName, opt => opt.Ignore()) // Resolved in service
                .ForMember(dest => dest.ExitCode, opt => opt.MapFrom(src => src.Results.ExitCode))
                .ForMember(dest => dest.HasError, opt => opt.MapFrom(src => !string.IsNullOrEmpty(src.Results.Error)))
                .ForMember(dest => dest.Duration, opt => opt.MapFrom(src => src.CompletedAt.HasValue
                    ? (src.CompletedAt.Value - src.StartedAt).TotalMinutes
                    : (double?)null));

            CreateMap<Execution, ExecutionDetailDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.ProgramId, opt => opt.MapFrom(src => src.ProgramId.ToString()))
                .ForMember(dest => dest.VersionId, opt => opt.MapFrom(src => src.VersionId.ToString()))
                .ForMember(dest => dest.ProgramName, opt => opt.Ignore()) // Resolved in service
                .ForMember(dest => dest.UserName, opt => opt.Ignore()) // Resolved in service
                .ForMember(dest => dest.VersionNumber, opt => opt.Ignore()) // Resolved in service
                .ForMember(dest => dest.RecentLogs, opt => opt.Ignore()) // Resolved in service
                .ForMember(dest => dest.Environment, opt => opt.Ignore()) // Resolved in service
                .ForMember(dest => dest.WebAppUrl, opt => opt.MapFrom(src => src.Results.WebAppUrl))
                .ForMember(dest => dest.WebAppStatus, opt => opt.Ignore()); // Resolved in service

            CreateMap<Execution, ExecutionStatusDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.Progress, opt => opt.Ignore()) // Calculated in service
                .ForMember(dest => dest.CurrentStep, opt => opt.Ignore()) // Set in service
                .ForMember(dest => dest.StatusMessage, opt => opt.Ignore()); // Set in service

            // Result mappings
            CreateMap<ExecutionResults, ExecutionResultDto>()
                .ForMember(dest => dest.CompletedAt, opt => opt.Ignore()); // Mapped from parent execution

            CreateMap<ResourceUsage, ExecutionResourceUsageDto>()
                .ForMember(dest => dest.CpuPercentage, opt => opt.Ignore()) // Calculated in service
                .ForMember(dest => dest.MemoryPercentage, opt => opt.Ignore()) // Calculated in service
                .ForMember(dest => dest.DiskPercentage, opt => opt.Ignore()) // Calculated in service
                .ForMember(dest => dest.LastUpdated, opt => opt.MapFrom(src => DateTime.UtcNow));

            // Resource limits mapping
            CreateMap<ExecutionResourceLimitsDto, ResourceUsage>()
                .ForMember(dest => dest.CpuTime, opt => opt.Ignore())
                .ForMember(dest => dest.MemoryUsed, opt => opt.Ignore())
                .ForMember(dest => dest.DiskUsed, opt => opt.Ignore());

            // Web app deployment mapping
            CreateMap<WebAppDeploymentRequestDto, Execution>()
                .ForMember(dest => dest._ID, opt => opt.Ignore())
                .ForMember(dest => dest.ProgramId, opt => opt.Ignore())
                .ForMember(dest => dest.VersionId, opt => opt.Ignore())
                .ForMember(dest => dest.UserId, opt => opt.Ignore())
                .ForMember(dest => dest.ExecutionType, opt => opt.MapFrom(src => "web_app_deploy"))
                .ForMember(dest => dest.StartedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => "running"))
                .ForMember(dest => dest.Parameters, opt => opt.MapFrom(src => src.Configuration))
                .ForMember(dest => dest.Results, opt => opt.MapFrom(src => new ExecutionResults()))
                .ForMember(dest => dest.ResourceUsage, opt => opt.MapFrom(src => new ResourceUsage()));

            // Statistics mappings
            CreateMap<List<Execution>, ExecutionStatsDto>()
                .ConvertUsing(src => new ExecutionStatsDto
                {
                    TotalExecutions = src.Count,
                    SuccessfulExecutions = src.Count(e => e.Status == "completed" && e.Results.ExitCode == 0),
                    FailedExecutions = src.Count(e => e.Status == "failed" || e.Results.ExitCode != 0),
                    RunningExecutions = src.Count(e => e.Status == "running"),
                    AverageExecutionTime = src.Where(e => e.CompletedAt.HasValue)
                        .Select(e => (e.CompletedAt!.Value - e.StartedAt).TotalMinutes)
                        .DefaultIfEmpty(0)
                        .Average(),
                    SuccessRate = src.Count > 0
                        ? (double)src.Count(e => e.Status == "completed" && e.Results.ExitCode == 0) / src.Count * 100
                        : 0,
                    TotalCpuTime = (long)src.Sum(e => e.ResourceUsage.CpuTime),
                    TotalMemoryUsed = src.Sum(e => e.ResourceUsage.MemoryUsed),
                    ExecutionsByStatus = src.GroupBy(e => e.Status).ToDictionary(g => g.Key, g => g.Count()),
                    ExecutionsByType = src.GroupBy(e => e.ExecutionType).ToDictionary(g => g.Key, g => g.Count())
                });
        }
    }
}