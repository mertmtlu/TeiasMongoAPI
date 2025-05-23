﻿using AutoMapper;
using DnsClient;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Request.Search;
using TeiasMongoAPI.Services.DTOs.Request.TM;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.DTOs.Response.Hazard;
using TeiasMongoAPI.Services.DTOs.Response.TM;
using TeiasMongoAPI.Services.Interfaces;
using TeiasMongoAPI.Services.Services.Base;
using ZstdSharp.Unsafe;

namespace TeiasMongoAPI.Services.Services.Implementations
{
    public class TMService : BaseService, ITMService
    {
        public TMService(IUnitOfWork unitOfWork, IMapper mapper, ILogger<TMService> logger)
            : base(unitOfWork, mapper, logger)
        {
        }

        public async Task<TMDetailResponseDto> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var tm = await _unitOfWork.TMs.GetByIdAsync(objectId, cancellationToken);

            if (tm == null)
            {
                throw new KeyNotFoundException($"TM with ID {id} not found.");
            }

            var dto = _mapper.Map<TMDetailResponseDto>(tm);

            // Get region info
            var region = await _unitOfWork.Regions.GetByIdAsync(tm.RegionID, cancellationToken);
            dto.Region = _mapper.Map<TeiasMongoAPI.Services.DTOs.Response.Region.RegionSummaryResponseDto>(region);

            // Get buildings
            var buildings = await _unitOfWork.Buildings.GetByTmIdAsync(objectId, cancellationToken);
            dto.BuildingCount = buildings.Count();
            dto.Buildings = _mapper.Map<List<DTOs.Response.Building.BuildingSummaryResponseDto>>(buildings);

            // Get alternative TMs
            var alternativeTMs = await _unitOfWork.AlternativeTMs.GetByTmIdAsync(objectId, cancellationToken);
            dto.AlternativeTMs = _mapper.Map<List<DTOs.Response.AlternativeTM.AlternativeTMSummaryResponseDto>>(alternativeTMs);

            return dto;
        }

        public async Task<PagedResponse<TMListResponseDto>> GetAllAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var tms = await _unitOfWork.TMs.GetAllAsync(cancellationToken);
            var tmsList = tms.ToList();

            // Apply sorting and pagination
            var totalCount = tmsList.Count;
            var paginatedTMs = tmsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = new List<TMListResponseDto>();
            foreach (var tm in paginatedTMs)
            {
                var dto = _mapper.Map<TMListResponseDto>(tm);

                // Get region name
                var region = await _unitOfWork.Regions.GetByIdAsync(tm.RegionID, cancellationToken);
                dto.RegionName = region?.Headquarters ?? "Unknown";

                // Get building count
                var buildings = await _unitOfWork.Buildings.GetByTmIdAsync(tm._ID, cancellationToken);
                dto.BuildingCount = buildings.Count();

                dtos.Add(dto);
            }

            return new PagedResponse<TMListResponseDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<PagedResponse<TMListResponseDto>> GetByRegionIdAsync(string regionId, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var regionObjectId = ParseObjectId(regionId);
            var tms = await _unitOfWork.TMs.GetByRegionIdAsync(regionObjectId, cancellationToken);
            var tmsList = tms.ToList();

            // Apply pagination
            var totalCount = tmsList.Count;
            var paginatedTMs = tmsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = new List<TMListResponseDto>();
            var region = await _unitOfWork.Regions.GetByIdAsync(regionObjectId, cancellationToken);

            foreach (var tm in paginatedTMs)
            {
                var dto = _mapper.Map<TMListResponseDto>(tm);
                dto.RegionName = region?.Headquarters ?? "Unknown";

                // Get building count
                var buildings = await _unitOfWork.Buildings.GetByTmIdAsync(tm._ID, cancellationToken);
                dto.BuildingCount = buildings.Count();

                dtos.Add(dto);
            }

            return new PagedResponse<TMListResponseDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<PagedResponse<TMListResponseDto>> SearchAsync(TMSearchDto searchDto, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var allTMs = await _unitOfWork.TMs.GetAllAsync(cancellationToken);
            var filteredTMs = allTMs.AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(searchDto.Name))
            {
                filteredTMs = filteredTMs.Where(tm => tm.Name.Contains(searchDto.Name, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(searchDto.RegionId))
            {
                var regionId = ParseObjectId(searchDto.RegionId);
                filteredTMs = filteredTMs.Where(tm => tm.RegionID == regionId);
            }

            if (searchDto.Type.HasValue)
            {
                filteredTMs = filteredTMs.Where(tm => tm.Type == searchDto.Type.Value);
            }

            if (searchDto.State.HasValue)
            {
                filteredTMs = filteredTMs.Where(tm => tm.State == searchDto.State.Value);
            }

            if (searchDto.Voltages?.Any() == true)
            {
                filteredTMs = filteredTMs.Where(tm => tm.Voltages.Any(v => searchDto.Voltages.Contains(v)));
            }

            if (!string.IsNullOrEmpty(searchDto.City))
            {
                filteredTMs = filteredTMs.Where(tm => tm.City.Contains(searchDto.City, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(searchDto.County))
            {
                filteredTMs = filteredTMs.Where(tm => tm.County.Contains(searchDto.County, StringComparison.OrdinalIgnoreCase));
            }

            if (searchDto.MaxVoltage.HasValue)
            {
                filteredTMs = filteredTMs.Where(tm => tm.MaxVoltage == searchDto.MaxVoltage.Value);
            }

            if (searchDto.ProvisionalAcceptanceDateFrom.HasValue)
            {
                var fromDate = searchDto.ProvisionalAcceptanceDateFrom.Value.ToDateTime(TimeOnly.MinValue);
                filteredTMs = filteredTMs.Where(tm => tm.ProvisionalAcceptanceDate >= fromDate);
            }

            if (searchDto.ProvisionalAcceptanceDateTo.HasValue)
            {
                var toDate = searchDto.ProvisionalAcceptanceDateTo.Value.ToDateTime(TimeOnly.MaxValue);
                filteredTMs = filteredTMs.Where(tm => tm.ProvisionalAcceptanceDate <= toDate);
            }

            var tmsList = filteredTMs.ToList();

            // Apply pagination
            var totalCount = tmsList.Count;
            var paginatedTMs = tmsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = new List<TMListResponseDto>();
            foreach (var tm in paginatedTMs)
            {
                var dto = _mapper.Map<TMListResponseDto>(tm);

                // Get region name
                var region = await _unitOfWork.Regions.GetByIdAsync(tm.RegionID, cancellationToken);
                dto.RegionName = region?.Headquarters ?? "Unknown";

                // Get building count
                var buildings = await _unitOfWork.Buildings.GetByTmIdAsync(tm._ID, cancellationToken);
                dto.BuildingCount = buildings.Count();

                dtos.Add(dto);
            }

            return new PagedResponse<TMListResponseDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<TMResponseDto> CreateAsync(TMCreateDto dto, CancellationToken cancellationToken = default)
        {
            // Validate region exists
            var regionId = ParseObjectId(dto.RegionId);
            var region = await _unitOfWork.Regions.GetByIdAsync(regionId, cancellationToken);
            if (region == null)
            {
                throw new InvalidOperationException($"Region with ID {dto.RegionId} not found.");
            }

            // Check if TM with same name exists
            var existingTM = await _unitOfWork.TMs.GetByNameAsync(dto.Name, cancellationToken);
            if (existingTM != null)
            {
                throw new InvalidOperationException($"TM with name '{dto.Name}' already exists.");
            }

            var tm = _mapper.Map<TM>(dto);
            var createdTM = await _unitOfWork.TMs.CreateAsync(tm, cancellationToken);

            return _mapper.Map<TMResponseDto>(createdTM);
        }

        public async Task<TMResponseDto> UpdateAsync(string id, TMUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var existingTM = await _unitOfWork.TMs.GetByIdAsync(objectId, cancellationToken);

            if (existingTM == null)
            {
                throw new KeyNotFoundException($"TM with ID {id} not found.");
            }

            // If updating region, validate it exists
            if (!string.IsNullOrEmpty(dto.RegionId))
            {
                var regionId = ParseObjectId(dto.RegionId);
                var region = await _unitOfWork.Regions.GetByIdAsync(regionId, cancellationToken);
                if (region == null)
                {
                    throw new InvalidOperationException($"Region with ID {dto.RegionId} not found.");
                }
            }

            // If updating name, check uniqueness
            if (!string.IsNullOrEmpty(dto.Name) && dto.Name != existingTM.Name)
            {
                var tmWithSameName = await _unitOfWork.TMs.GetByNameAsync(dto.Name, cancellationToken);
                if (tmWithSameName != null)
                {
                    throw new InvalidOperationException($"TM with name '{dto.Name}' already exists.");
                }
            }

            _mapper.Map(dto, existingTM);
            var success = await _unitOfWork.TMs.UpdateAsync(objectId, existingTM, cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException($"Failed to update TM with ID {id}.");
            }

            return _mapper.Map<TMResponseDto>(existingTM);
        }

        public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var tm = await _unitOfWork.TMs.GetByIdAsync(objectId, cancellationToken);

            if (tm == null)
            {
                throw new KeyNotFoundException($"TM with ID {id} not found.");
            }

            // Check dependencies
            var buildings = await _unitOfWork.Buildings.GetByTmIdAsync(objectId, cancellationToken);
            if (buildings.Any())
            {
                throw new InvalidOperationException($"Cannot delete TM with ID {id} because it has {buildings.Count()} associated buildings.");
            }

            var alternativeTMs = await _unitOfWork.AlternativeTMs.GetByTmIdAsync(objectId, cancellationToken);
            if (alternativeTMs.Any())
            {
                throw new InvalidOperationException($"Cannot delete TM with ID {id} because it has {alternativeTMs.Count()} alternative TMs.");
            }

            return await _unitOfWork.TMs.DeleteAsync(objectId, cancellationToken);
        }

        public async Task<TMResponseDto> UpdateStateAsync(string id, TMStateUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var tm = await _unitOfWork.TMs.GetByIdAsync(objectId, cancellationToken);

            if (tm == null)
            {
                throw new KeyNotFoundException($"TM with ID {id} not found.");
            }

            tm.State = dto.State;
            var success = await _unitOfWork.TMs.UpdateAsync(objectId, tm, cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException($"Failed to update state for TM with ID {id}.");
            }

            return _mapper.Map<TMResponseDto>(tm);
        }

        public async Task<TMResponseDto> UpdateVoltagesAsync(string id, TMVoltageUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var tm = await _unitOfWork.TMs.GetByIdAsync(objectId, cancellationToken);

            if (tm == null)
            {
                throw new KeyNotFoundException($"TM with ID {id} not found.");
            }

            tm.Voltages = dto.Voltages;
            var success = await _unitOfWork.TMs.UpdateAsync(objectId, tm, cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException($"Failed to update voltages for TM with ID {id}.");
            }

            return _mapper.Map<TMResponseDto>(tm);
        }

        public async Task<TMResponseDto> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            var tm = await _unitOfWork.TMs.GetByNameAsync(name, cancellationToken);

            if (tm == null)
            {
                throw new KeyNotFoundException($"TM with name '{name}' not found.");
            }
            return _mapper.Map<TMResponseDto>(tm);
        }

        public async Task<TMStatisticsResponseDto> GetStatisticsAsync(string id, CancellationToken cancellationToken = default)
        {
            var tm = await GetByIdAsync(id, cancellationToken);

            return new TMStatisticsResponseDto
            {
                TMId = id,
                BuildingCount = tm.BuildingCount,
                MaxVoltage = tm.MaxVoltage,
                AlternativeTMCount = tm.AlternativeTMs.Count,
                OverallRiskScore = CalculateOverallRiskScore(tm),
                DaysSinceAcceptance = (DateTime.Now - tm.ProvisionalAcceptanceDate).Days
            };
        }

        public async Task<TMHazardSummaryResponseDto> GetHazardSummaryAsync(string id, CancellationToken cancellationToken = default)
        {
            var tm = await GetByIdAsync(id, cancellationToken);

            return new TMHazardSummaryResponseDto
            {
                TMId = id,
                FireHazard = new HazardResponseDto
                {
                    Score = tm.FireHazard.Score,
                    Level = tm.FireHazard.Level.ToString(),
                    Description = tm.FireHazard.PreviousIncidentDescription
                },
                SecurityHazard = new HazardResponseDto
                {
                    Score = tm.SecurityHazard.Score,
                    Level = tm.SecurityHazard.Level.ToString(),
                    HasCCTV = tm.SecurityHazard.HasCCTV
                },
                FloodHazard = new HazardResponseDto
                {
                    Score = tm.FloodHazard.Score,
                    Level = tm.FloodHazard.Level.ToString(),
                    Description = tm.FloodHazard.IncidentDescription
                },
                OverallRiskScore = CalculateOverallRiskScore(tm)
            };
        }

        private double CalculateOverallRiskScore(TMDetailResponseDto tm)
        {
            var scores = new[]
            {
                tm.FireHazard.Score,
                tm.SecurityHazard.Score,
                tm.NoiseHazard.Score,
                tm.AvalancheHazard.Score,
                tm.LandslideHazard.Score,
                tm.RockFallHazard.Score,
                tm.FloodHazard.Score,
                tm.TsunamiHazard.Score
            };
            return scores.Max();
        }
    }
}