using FluentValidation;
using MongoDB.Bson;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Services.DTOs.Request.TM;
using TeiasMongoAPI.Services.Validators.Common;
using TeiasMongoAPI.Services.Validators.Hazard;

namespace TeiasMongoAPI.Services.Validators.TM
{
    public class TMCreateDtoValidator : AbstractValidator<TMCreateDto>
    {
        private readonly IUnitOfWork _unitOfWork;

        public TMCreateDtoValidator(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;

            RuleFor(x => x.RegionId)
                .NotEmpty().WithMessage("Region ID is required")
                .Must(BeValidObjectId).WithMessage("Invalid Region ID format")
                .MustAsync(RegionExists).WithMessage("Region not found");

            RuleFor(x => x.TmID)
                .GreaterThan(0).WithMessage("TM ID must be greater than 0");

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Name is required")
                .MaximumLength(200).WithMessage("Name cannot exceed 200 characters")
                .MustAsync(BeUniqueName).WithMessage("TM name already exists");

            RuleFor(x => x.Voltages)
                .NotEmpty().WithMessage("At least one voltage is required")
                .Must(HaveValidVoltages).WithMessage("All voltages must be positive");

            RuleFor(x => x.Location)
                .NotNull().WithMessage("Location is required")
                .SetValidator(new LocationRequestDtoValidator());

            RuleFor(x => x.ProvisionalAcceptanceDate)
                .Must(BeValidDate).When(x => x.ProvisionalAcceptanceDate.HasValue)
                .WithMessage("Provisional acceptance date must not be in the future");

            // Earthquake level validators
            RuleFor(x => x.DD1).NotNull().WithMessage("DD1 is required")
                .SetValidator(new EarthquakeLevelDtoValidator());
            RuleFor(x => x.DD2).NotNull().WithMessage("DD2 is required")
                .SetValidator(new EarthquakeLevelDtoValidator());
            RuleFor(x => x.DD3).NotNull().WithMessage("DD3 is required")
                .SetValidator(new EarthquakeLevelDtoValidator());

            When(x => x.EarthquakeScenario != null, () =>
            {
                RuleFor(x => x.EarthquakeScenario)
                    .SetValidator(new EarthquakeLevelDtoValidator());
            });

            // Hazard validators
            RuleFor(x => x.Pollution).NotNull().WithMessage("Pollution data is required")
                .SetValidator(new PollutionDtoValidator());
            RuleFor(x => x.FireHazard).NotNull().WithMessage("Fire hazard data is required")
                .SetValidator(new FireHazardDtoValidator());
            RuleFor(x => x.SecurityHazard).NotNull().WithMessage("Security hazard data is required")
                .SetValidator(new SecurityHazardDtoValidator());
            RuleFor(x => x.NoiseHazard).NotNull().WithMessage("Noise hazard data is required")
                .SetValidator(new NoiseHazardDtoValidator());
            RuleFor(x => x.AvalancheHazard).NotNull().WithMessage("Avalanche hazard data is required")
                .SetValidator(new AvalancheHazardDtoValidator());
            RuleFor(x => x.LandslideHazard).NotNull().WithMessage("Landslide hazard data is required")
                .SetValidator(new LandslideHazardDtoValidator());
            RuleFor(x => x.RockFallHazard).NotNull().WithMessage("Rock fall hazard data is required")
                .SetValidator(new RockFallHazardDtoValidator());
            RuleFor(x => x.FloodHazard).NotNull().WithMessage("Flood hazard data is required")
                .SetValidator(new FloodHazardDtoValidator());
            RuleFor(x => x.TsunamiHazard).NotNull().WithMessage("Tsunami hazard data is required")
                .SetValidator(new TsunamiHazardDtoValidator());

            RuleFor(x => x.Soil).NotNull().WithMessage("Soil data is required")
                .SetValidator(new SoilDtoValidator());
        }

        private bool BeValidObjectId(string objectId)
        {
            return ObjectId.TryParse(objectId, out _);
        }

        private async Task<bool> RegionExists(string regionId, CancellationToken cancellationToken)
        {
            if (!ObjectId.TryParse(regionId, out var objectId))
                return false;

            return await _unitOfWork.Regions.ExistsAsync(objectId, cancellationToken);
        }

        private async Task<bool> BeUniqueName(string name, CancellationToken cancellationToken)
        {
            var existingTM = await _unitOfWork.TMs.GetByNameAsync(name, cancellationToken);
            return existingTM == null;
        }

        private bool HaveValidVoltages(List<int> voltages)
        {
            return voltages.All(v => v > 0);
        }

        private bool BeValidDate(DateOnly? date)
        {
            if (!date.HasValue) return true;
            return date.Value <= DateOnly.FromDateTime(DateTime.UtcNow);
        }
    }
}