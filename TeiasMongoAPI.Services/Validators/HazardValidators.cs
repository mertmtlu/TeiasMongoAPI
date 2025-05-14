using FluentValidation;
using TeiasMongoAPI.Services.DTOs.Request.Hazard;
using TeiasMongoAPI.Services.Validators.Common;

namespace TeiasMongoAPI.Services.Validators.Hazard
{
    public class PollutionDtoValidator : AbstractValidator<PollutionDto>
    {
        public PollutionDtoValidator()
        {
            RuleFor(x => x.PollutantLocation)
                .NotNull().WithMessage("Pollutant location is required")
                .SetValidator(new LocationRequestDtoValidator());

            RuleFor(x => x.PollutantNo)
                .GreaterThanOrEqualTo(0).WithMessage("Pollutant number must be non-negative");

            RuleFor(x => x.PollutantDistance)
                .GreaterThanOrEqualTo(0).WithMessage("Pollutant distance must be non-negative");

            RuleFor(x => x.PollutantLevel)
                .IsInEnum().WithMessage("Invalid pollutant level");
        }
    }

    public class FireHazardDtoValidator : AbstractValidator<FireHazardDto>
    {
        public FireHazardDtoValidator()
        {
            // Base hazard validations
            RuleFor(x => x.Score)
                .InclusiveBetween(0, 1).WithMessage("Score must be between 0 and 1");

            RuleFor(x => x.Level)
                .IsInEnum().WithMessage("Invalid hazard level");

            RuleFor(x => x.DistanceToInventory)
                .GreaterThanOrEqualTo(0).WithMessage("Distance to inventory must be non-negative");

            When(x => x.PreviousIncidentOccurred, () =>
            {
                RuleFor(x => x.PreviousIncidentDescription)
                    .NotEmpty().WithMessage("Previous incident description is required when incident occurred");
            });

            // Fire hazard specific validations
            RuleFor(x => x.DistanceToNearbyGasStation)
                .GreaterThanOrEqualTo(0).WithMessage("Distance to gas station must be non-negative");

            RuleFor(x => x.IndustrialFireExposedFacade)
                .GreaterThanOrEqualTo(0).WithMessage("Exposed facade must be non-negative");

            RuleFor(x => x.DistanceToClosestForest)
                .GreaterThanOrEqualTo(0).WithMessage("Distance to forest must be non-negative");

            When(x => x.ExternalFireIncident, () =>
            {
                RuleFor(x => x.ExternalFireIncidentDescription)
                    .NotEmpty().WithMessage("External fire incident description is required");
            });
        }
    }

    public class SecurityHazardDtoValidator : AbstractValidator<SecurityHazardDto>
    {
        public SecurityHazardDtoValidator()
        {
            // Base hazard validations
            RuleFor(x => x.Score)
                .InclusiveBetween(0, 1).WithMessage("Score must be between 0 and 1");

            RuleFor(x => x.Level)
                .IsInEnum().WithMessage("Invalid hazard level");

            RuleFor(x => x.DistanceToInventory)
                .GreaterThanOrEqualTo(0).WithMessage("Distance to inventory must be non-negative");

            When(x => x.PreviousIncidentOccurred, () =>
            {
                RuleFor(x => x.PreviousIncidentDescription)
                    .NotEmpty().WithMessage("Previous incident description is required when incident occurred");
            });

            // Security hazard specific validations
            RuleFor(x => x.SecuritySystemScore)
                .GreaterThanOrEqualTo(0).WithMessage("Security system score must be non-negative");

            RuleFor(x => x.EGMRiskLevel)
                .GreaterThanOrEqualTo(0).WithMessage("EGM risk level must be non-negative");

            RuleFor(x => x.EGMRiskLevelScore)
                .GreaterThanOrEqualTo(0).WithMessage("EGM risk level score must be non-negative");

            RuleFor(x => x.PerimeterWallTypeScore)
                .GreaterThanOrEqualTo(0).WithMessage("Perimeter wall type score must be non-negative");

            RuleFor(x => x.CCTVConditionScore)
                .GreaterThanOrEqualTo(0).WithMessage("CCTV condition score must be non-negative");

            RuleFor(x => x.IEMDistance)
                .GreaterThanOrEqualTo(0).WithMessage("IEM distance must be non-negative");

            RuleFor(x => x.IEMDistanceScore)
                .GreaterThanOrEqualTo(0).WithMessage("IEM distance score must be non-negative");

            RuleFor(x => x.PerimeterFenceType)
                .IsInEnum().WithMessage("Invalid perimeter fence type");

            RuleFor(x => x.WallCondition)
                .IsInEnum().WithMessage("Invalid wall condition");
        }
    }

    public class NoiseHazardDtoValidator : AbstractValidator<NoiseHazardDto>
    {
        public NoiseHazardDtoValidator()
        {
            // Base hazard validations
            RuleFor(x => x.Score)
                .InclusiveBetween(0, 1).WithMessage("Score must be between 0 and 1");

            RuleFor(x => x.Level)
                .IsInEnum().WithMessage("Invalid hazard level");

            RuleFor(x => x.DistanceToInventory)
                .GreaterThanOrEqualTo(0).WithMessage("Distance to inventory must be non-negative");

            When(x => x.PreviousIncidentOccurred, () =>
            {
                RuleFor(x => x.PreviousIncidentDescription)
                    .NotEmpty().WithMessage("Previous incident description is required when incident occurred");
            });

            // Noise hazard specific validations
            When(x => x.NoiseMeasurementsForBuildings != null, () =>
            {
                RuleForEach(x => x.NoiseMeasurementsForBuildings.Values)
                    .GreaterThanOrEqualTo(0).WithMessage("All noise measurements must be non-negative");
            });

            When(x => x.ExtremeNoise, () =>
            {
                RuleFor(x => x.ExtremeNoiseDescription)
                    .NotEmpty().WithMessage("Extreme noise description is required");
            });
        }
    }

    public class AvalancheHazardDtoValidator : AbstractValidator<AvalancheHazardDto>
    {
        public AvalancheHazardDtoValidator()
        {
            // Base hazard validations
            RuleFor(x => x.Score)
                .InclusiveBetween(0, 1).WithMessage("Score must be between 0 and 1");

            RuleFor(x => x.Level)
                .IsInEnum().WithMessage("Invalid hazard level");

            RuleFor(x => x.DistanceToInventory)
                .GreaterThanOrEqualTo(0).WithMessage("Distance to inventory must be non-negative");

            When(x => x.PreviousIncidentOccurred, () =>
            {
                RuleFor(x => x.PreviousIncidentDescription)
                    .NotEmpty().WithMessage("Previous incident description is required when incident occurred");
            });

            // Avalanche hazard specific validations
            RuleFor(x => x.SnowDepth)
                .GreaterThanOrEqualTo(0).WithMessage("Snow depth must be non-negative");

            RuleFor(x => x.ElevationDifference)
                .GreaterThanOrEqualTo(0).WithMessage("Elevation difference must be non-negative");

            When(x => x.FirstHillLocation != null, () =>
            {
                RuleFor(x => x.FirstHillLocation)
                    .SetValidator(new LocationRequestDtoValidator());
            });
        }
    }

    public class LandslideHazardDtoValidator : AbstractValidator<LandslideHazardDto>
    {
        public LandslideHazardDtoValidator()
        {
            // Base hazard validations
            RuleFor(x => x.Score)
                .InclusiveBetween(0, 1).WithMessage("Score must be between 0 and 1");

            RuleFor(x => x.Level)
                .IsInEnum().WithMessage("Invalid hazard level");

            RuleFor(x => x.DistanceToInventory)
                .GreaterThanOrEqualTo(0).WithMessage("Distance to inventory must be non-negative");

            When(x => x.PreviousIncidentOccurred, () =>
            {
                RuleFor(x => x.PreviousIncidentDescription)
                    .NotEmpty().WithMessage("Previous incident description is required when incident occurred");
            });
        }
    }

    public class RockFallHazardDtoValidator : AbstractValidator<RockFallHazardDto>
    {
        public RockFallHazardDtoValidator()
        {
            // Base hazard validations
            RuleFor(x => x.Score)
                .InclusiveBetween(0, 1).WithMessage("Score must be between 0 and 1");

            RuleFor(x => x.Level)
                .IsInEnum().WithMessage("Invalid hazard level");

            RuleFor(x => x.DistanceToInventory)
                .GreaterThanOrEqualTo(0).WithMessage("Distance to inventory must be non-negative");

            When(x => x.PreviousIncidentOccurred, () =>
            {
                RuleFor(x => x.PreviousIncidentDescription)
                    .NotEmpty().WithMessage("Previous incident description is required when incident occurred");
            });
        }
    }

    public class FloodHazardDtoValidator : AbstractValidator<FloodHazardDto>
    {
        public FloodHazardDtoValidator()
        {
            // Base hazard validations
            RuleFor(x => x.Score)
                .InclusiveBetween(0, 1).WithMessage("Score must be between 0 and 1");

            RuleFor(x => x.Level)
                .IsInEnum().WithMessage("Invalid hazard level");

            RuleFor(x => x.DistanceToInventory)
                .GreaterThanOrEqualTo(0).WithMessage("Distance to inventory must be non-negative");

            When(x => x.PreviousIncidentOccurred, () =>
            {
                RuleFor(x => x.PreviousIncidentDescription)
                    .NotEmpty().WithMessage("Previous incident description is required when incident occurred");
            });
        }
    }

    public class TsunamiHazardDtoValidator : AbstractValidator<TsunamiHazardDto>
    {
        public TsunamiHazardDtoValidator()
        {
            // Base hazard validations
            RuleFor(x => x.Score)
                .InclusiveBetween(0, 1).WithMessage("Score must be between 0 and 1");

            RuleFor(x => x.Level)
                .IsInEnum().WithMessage("Invalid hazard level");

            RuleFor(x => x.DistanceToInventory)
                .GreaterThanOrEqualTo(0).WithMessage("Distance to inventory must be non-negative");

            When(x => x.PreviousIncidentOccurred, () =>
            {
                RuleFor(x => x.PreviousIncidentDescription)
                    .NotEmpty().WithMessage("Previous incident description is required when incident occurred");
            });
        }
    }
}