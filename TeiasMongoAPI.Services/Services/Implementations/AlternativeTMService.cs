using AutoMapper;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.AlternativeTM;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Response.AlternativeTM;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.DTOs.Response.Hazard;
using RequestLocationDto = TeiasMongoAPI.Services.DTOs.Request.Common.LocationDto;
using RequestAddressDto = TeiasMongoAPI.Services.DTOs.Request.Common.AddressDto;
using TeiasMongoAPI.Services.Interfaces;
using TeiasMongoAPI.Services.Services.Base;
using Microsoft.Extensions.Logging;

namespace TeiasMongoAPI.Services.Services.Implementations
{
    public class AlternativeTMService : BaseService, IAlternativeTMService
    {
        public AlternativeTMService(IUnitOfWork unitOfWork, IMapper mapper, ILogger<AlternativeTMService> logger)
            : base(unitOfWork, mapper, logger)
        {
        }

        public async Task<AlternativeTMDetailDto> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var alternativeTM = await _unitOfWork.AlternativeTMs.GetByIdAsync(objectId, cancellationToken);

            if (alternativeTM == null)
            {
                throw new KeyNotFoundException($"Alternative TM with ID {id} not found.");
            }

            var dto = _mapper.Map<AlternativeTMDetailDto>(alternativeTM);

            // Get TM info
            var tm = await _unitOfWork.TMs.GetByIdAsync(alternativeTM.TmID, cancellationToken);
            dto.TM = _mapper.Map<TeiasMongoAPI.Services.DTOs.Response.TM.TMSummaryDto>(tm);

            // Calculate hazard summary
            dto.HazardSummary = CalculateHazardSummary(alternativeTM);

            return dto;
        }

        public async Task<PagedResponse<AlternativeTMSummaryDto>> GetByTmIdAsync(string tmId, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var tmObjectId = ParseObjectId(tmId);
            var alternativeTMs = await _unitOfWork.AlternativeTMs.GetByTmIdAsync(tmObjectId, cancellationToken);
            var alternativeTMsList = alternativeTMs.ToList();

            // Apply pagination
            var totalCount = alternativeTMsList.Count;
            var paginatedAlternatives = alternativeTMsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = _mapper.Map<List<AlternativeTMSummaryDto>>(paginatedAlternatives);

            // Calculate overall risk scores
            foreach (var dto in dtos)
            {
                var alternative = alternativeTMsList.First(a => a._ID.ToString() == dto.Id);
                dto.OverallRiskScore = CalculateOverallRiskScore(alternative);
            }

            return new PagedResponse<AlternativeTMSummaryDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<AlternativeTMDto> CreateAsync(AlternativeTMCreateDto dto, CancellationToken cancellationToken = default)
        {
            // Validate TM exists
            var tmId = ParseObjectId(dto.TmId);
            var tm = await _unitOfWork.TMs.GetByIdAsync(tmId, cancellationToken);
            if (tm == null)
            {
                throw new InvalidOperationException($"TM with ID {dto.TmId} not found.");
            }

            var alternativeTM = _mapper.Map<AlternativeTM>(dto);
            var createdAlternativeTM = await _unitOfWork.AlternativeTMs.CreateAsync(alternativeTM, cancellationToken);

            return _mapper.Map<AlternativeTMDto>(createdAlternativeTM);
        }

        public async Task<AlternativeTMDto> UpdateAsync(string id, AlternativeTMUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var existingAlternativeTM = await _unitOfWork.AlternativeTMs.GetByIdAsync(objectId, cancellationToken);

            if (existingAlternativeTM == null)
            {
                throw new KeyNotFoundException($"Alternative TM with ID {id} not found.");
            }

            // If updating TM, validate it exists
            if (!string.IsNullOrEmpty(dto.TmId))
            {
                var tmId = ParseObjectId(dto.TmId);
                var tm = await _unitOfWork.TMs.GetByIdAsync(tmId, cancellationToken);
                if (tm == null)
                {
                    throw new InvalidOperationException($"TM with ID {dto.TmId} not found.");
                }
            }

            _mapper.Map(dto, existingAlternativeTM);
            var success = await _unitOfWork.AlternativeTMs.UpdateAsync(objectId, existingAlternativeTM, cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException($"Failed to update alternative TM with ID {id}.");
            }

            return _mapper.Map<AlternativeTMDto>(existingAlternativeTM);
        }

        public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var alternativeTM = await _unitOfWork.AlternativeTMs.GetByIdAsync(objectId, cancellationToken);

            if (alternativeTM == null)
            {
                throw new KeyNotFoundException($"Alternative TM with ID {id} not found.");
            }

            return await _unitOfWork.AlternativeTMs.DeleteAsync(objectId, cancellationToken);
        }

        public async Task<List<AlternativeTMComparisonDto>> CompareAlternativesAsync(string tmId, CancellationToken cancellationToken = default)
        {
            var tmObjectId = ParseObjectId(tmId);
            var tm = await _unitOfWork.TMs.GetByIdAsync(tmObjectId, cancellationToken);

            if (tm == null)
            {
                throw new KeyNotFoundException($"TM with ID {tmId} not found.");
            }

            var alternatives = await _unitOfWork.AlternativeTMs.GetByTmIdAsync(tmObjectId, cancellationToken);
            var comparisonResults = new List<AlternativeTMComparisonDto>();

            foreach (var alternative in alternatives)
            {
                var comparison = new AlternativeTMComparisonDto
                {
                    Id = alternative._ID.ToString(),
                    Location = _mapper.Map<RequestLocationDto>(alternative.Location),
                    Address = _mapper.Map<RequestAddressDto>(new { alternative.City, alternative.County, alternative.District, alternative.Street }),
                    HazardSummary = CalculateHazardSummary(alternative),
                    DistanceFromOriginal = CalculateDistance(tm.Location, alternative.Location),
                    ComparisonScore = CalculateComparisonScore(tm, alternative)
                };

                comparisonResults.Add(comparison);
            }

            // Sort by overall improvement score
            return comparisonResults.OrderByDescending(c => c.ComparisonScore.OverallImprovement).ToList();
        }

        public async Task<PagedResponse<AlternativeTMSummaryDto>> GetByCityAsync(string city, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var alternativeTMs = await _unitOfWork.AlternativeTMs.GetByCityAsync(city, cancellationToken);
            var alternativeTMsList = alternativeTMs.ToList();

            // Apply pagination
            var totalCount = alternativeTMsList.Count;
            var paginatedAlternatives = alternativeTMsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = _mapper.Map<List<AlternativeTMSummaryDto>>(paginatedAlternatives);

            // Calculate overall risk scores
            foreach (var dto in dtos)
            {
                var alternative = alternativeTMsList.First(a => a._ID.ToString() == dto.Id);
                dto.OverallRiskScore = CalculateOverallRiskScore(alternative);
            }

            return new PagedResponse<AlternativeTMSummaryDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<PagedResponse<AlternativeTMSummaryDto>> GetByCountyAsync(string county, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var alternativeTMs = await _unitOfWork.AlternativeTMs.GetByCountyAsync(county, cancellationToken);
            var alternativeTMsList = alternativeTMs.ToList();

            // Apply pagination
            var totalCount = alternativeTMsList.Count;
            var paginatedAlternatives = alternativeTMsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = _mapper.Map<List<AlternativeTMSummaryDto>>(paginatedAlternatives);

            // Calculate overall risk scores
            foreach (var dto in dtos)
            {
                var alternative = alternativeTMsList.First(a => a._ID.ToString() == dto.Id);
                dto.OverallRiskScore = CalculateOverallRiskScore(alternative);
            }

            return new PagedResponse<AlternativeTMSummaryDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        private HazardSummaryDto CalculateHazardSummary(AlternativeTM tm)
        {
            var summary = new HazardSummaryDto
            {
                FireHazardScore = tm.FireHazard.Score,
                SecurityHazardScore = tm.SecurityHazard.Score,
                NoiseHazardScore = tm.NoiseHazard.Score,
                AvalancheHazardScore = tm.AvalancheHazard.Score,
                LandslideHazardScore = tm.LandslideHazard.Score,
                RockFallHazardScore = tm.RockFallHazard.Score,
                FloodHazardScore = tm.FloodHazard.Score,
                TsunamiHazardScore = tm.TsunamiHazard.Score
            };

            // Calculate overall risk score as weighted average
            var hazardScores = new[]
            {
                summary.FireHazardScore,
                summary.SecurityHazardScore,
                summary.NoiseHazardScore,
                summary.AvalancheHazardScore,
                summary.LandslideHazardScore,
                summary.RockFallHazardScore,
                summary.FloodHazardScore,
                summary.TsunamiHazardScore
            };

            summary.OverallRiskScore = hazardScores.Average();

            // Determine highest risk type
            var maxScore = hazardScores.Max();
            if (maxScore == summary.FireHazardScore) summary.HighestRiskType = "Fire";
            else if (maxScore == summary.SecurityHazardScore) summary.HighestRiskType = "Security";
            else if (maxScore == summary.NoiseHazardScore) summary.HighestRiskType = "Noise";
            else if (maxScore == summary.AvalancheHazardScore) summary.HighestRiskType = "Avalanche";
            else if (maxScore == summary.LandslideHazardScore) summary.HighestRiskType = "Landslide";
            else if (maxScore == summary.RockFallHazardScore) summary.HighestRiskType = "RockFall";
            else if (maxScore == summary.FloodHazardScore) summary.HighestRiskType = "Flood";
            else if (maxScore == summary.TsunamiHazardScore) summary.HighestRiskType = "Tsunami";

            return summary;
        }

        private double CalculateOverallRiskScore(AlternativeTM tm)
        {
            var hazardScores = new[]
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

            return hazardScores.Average();
        }

        private double CalculateOverallRiskScore(TM tm)
        {
            var hazardScores = new[]
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

            return hazardScores.Average();
        }

        private double CalculateDistance(Core.Models.Common.Location location1, Core.Models.Common.Location location2)
        {
            // Haversine formula for calculating distance between two points on Earth
            const double R = 6371; // Earth's radius in kilometers

            var lat1Rad = ToRadians(location1.Latitude);
            var lat2Rad = ToRadians(location2.Latitude);
            var deltaLatRad = ToRadians(location2.Latitude - location1.Latitude);
            var deltaLonRad = ToRadians(location2.Longitude - location1.Longitude);

            var a = Math.Sin(deltaLatRad / 2) * Math.Sin(deltaLatRad / 2) +
                    Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                    Math.Sin(deltaLonRad / 2) * Math.Sin(deltaLonRad / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return R * c;
        }

        private ComparisonScoreDto CalculateComparisonScore(TM original, AlternativeTM alternative)
        {
            var score = new ComparisonScoreDto();

            // Calculate earthquake improvement
            var originalEarthquakeScore = (original.DD1.PGA + original.DD2.PGA + original.DD3.PGA) / 3;
            var alternativeEarthquakeScore = (alternative.DD1.PGA + alternative.DD2.PGA + alternative.DD3.PGA) / 3;
            score.EarthquakeImprovement = ((originalEarthquakeScore - alternativeEarthquakeScore) / originalEarthquakeScore) * 100;

            // Calculate hazard improvement
            var originalHazardScore = CalculateOverallRiskScore(original);
            var alternativeHazardScore = CalculateOverallRiskScore(alternative);
            score.HazardImprovement = ((originalHazardScore - alternativeHazardScore) / originalHazardScore) * 100;

            // Calculate overall improvement
            score.OverallImprovement = (score.EarthquakeImprovement + score.HazardImprovement) / 2;

            // Determine advantages and disadvantages
            if (alternative.FireHazard.Score < original.FireHazard.Score)
                score.Advantages.Add("Lower fire hazard risk");
            else if (alternative.FireHazard.Score > original.FireHazard.Score)
                score.Disadvantages.Add("Higher fire hazard risk");

            if (alternative.FloodHazard.Score < original.FloodHazard.Score)
                score.Advantages.Add("Lower flood hazard risk");
            else if (alternative.FloodHazard.Score > original.FloodHazard.Score)
                score.Disadvantages.Add("Higher flood hazard risk");

            if (alternative.Soil.LiquefactionRisk != original.Soil.LiquefactionRisk)
            {
                if (!alternative.Soil.LiquefactionRisk)
                    score.Advantages.Add("No liquefaction risk");
                else
                    score.Disadvantages.Add("Has liquefaction risk");
            }

            return score;
        }

        private double ToRadians(double degrees)
        {
            return degrees * (Math.PI / 180);
        }
    }
}