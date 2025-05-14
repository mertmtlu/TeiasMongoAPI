using FluentValidation;
using MongoDB.Bson;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Services.DTOs.Request.Building;
using TeiasMongoAPI.Services.DTOs.Request.Block;

namespace TeiasMongoAPI.Services.Validators.Building
{
    public class BuildingCreateDtoValidator : AbstractValidator<BuildingCreateDto>
    {
        private readonly IUnitOfWork _unitOfWork;

        public BuildingCreateDtoValidator(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;

            RuleFor(x => x.TmId)
                .NotEmpty().WithMessage("TM ID is required")
                .Must(BeValidObjectId).WithMessage("Invalid TM ID format")
                .MustAsync(TMExists).WithMessage("TM not found");

            RuleFor(x => x.BuildingTMID)
                .GreaterThan(0).WithMessage("Building TM ID must be greater than 0")
                .MustAsync(BeUniqueBuildingTMID).WithMessage("Building TM ID already exists for this TM");

            RuleFor(x => x.Name)
                .MaximumLength(200).When(x => !string.IsNullOrEmpty(x.Name))
                .WithMessage("Name cannot exceed 200 characters");

            RuleFor(x => x.Type)
                .IsInEnum().WithMessage("Invalid building type");

            RuleFor(x => x.ReportName)
                .MaximumLength(500).When(x => !string.IsNullOrEmpty(x.ReportName))
                .WithMessage("Report name cannot exceed 500 characters");
        }

        private bool BeValidObjectId(string objectId)
        {
            return ObjectId.TryParse(objectId, out _);
        }

        private async Task<bool> TMExists(string tmId, CancellationToken cancellationToken)
        {
            if (!ObjectId.TryParse(tmId, out var objectId))
                return false;

            return await _unitOfWork.TMs.ExistsAsync(objectId, cancellationToken);
        }

        private async Task<bool> BeUniqueBuildingTMID(BuildingCreateDto dto, int buildingTMID, CancellationToken cancellationToken)
        {
            if (!ObjectId.TryParse(dto.TmId, out var tmId))
                return false;

            var buildingsInTM = await _unitOfWork.Buildings.GetByTmIdAsync(tmId, cancellationToken);
            return !buildingsInTM.Any(b => b.BuildingTMID == buildingTMID);
        }
    }
}

namespace TeiasMongoAPI.Services.Validators.Block
{
    public class ConcreteCreateDtoValidator : AbstractValidator<ConcreteCreateDto>
    {
        public ConcreteCreateDtoValidator()
        {
            RuleFor(x => x.ID)
                .NotEmpty().WithMessage("Block ID is required")
                .MaximumLength(50).WithMessage("Block ID cannot exceed 50 characters");

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Name is required")
                .MaximumLength(200).WithMessage("Name cannot exceed 200 characters");

            RuleFor(x => x.XAxisLength)
                .GreaterThan(0).WithMessage("X-axis length must be greater than 0");

            RuleFor(x => x.YAxisLength)
                .GreaterThan(0).WithMessage("Y-axis length must be greater than 0");

            RuleFor(x => x.StoreyHeight)
                .NotEmpty().WithMessage("Storey heights are required")
                .Must(HaveValidStoreyHeights).WithMessage("All storey heights must be greater than 0");

            RuleFor(x => x.CompressiveStrengthOfConcrete)
                .GreaterThan(0).WithMessage("Compressive strength must be greater than 0");

            RuleFor(x => x.YieldStrengthOfSteel)
                .GreaterThan(0).WithMessage("Yield strength must be greater than 0");

            RuleFor(x => x.TransverseReinforcementSpacing)
                .GreaterThan(0).WithMessage("Transverse reinforcement spacing must be greater than 0");

            RuleFor(x => x.ReinforcementRatio)
                .InclusiveBetween(0, 1).WithMessage("Reinforcement ratio must be between 0 and 1");
        }

        private bool HaveValidStoreyHeights(Dictionary<int, double> storeyHeights)
        {
            return storeyHeights.Values.All(h => h > 0);
        }
    }

    public class MasonryCreateDtoValidator : AbstractValidator<MasonryCreateDto>
    {
        public MasonryCreateDtoValidator()
        {
            RuleFor(x => x.ID)
                .NotEmpty().WithMessage("Block ID is required")
                .MaximumLength(50).WithMessage("Block ID cannot exceed 50 characters");

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Name is required")
                .MaximumLength(200).WithMessage("Name cannot exceed 200 characters");

            RuleFor(x => x.XAxisLength)
                .GreaterThan(0).WithMessage("X-axis length must be greater than 0");

            RuleFor(x => x.YAxisLength)
                .GreaterThan(0).WithMessage("Y-axis length must be greater than 0");

            RuleFor(x => x.StoreyHeight)
                .NotEmpty().WithMessage("Storey heights are required")
                .Must(HaveValidStoreyHeights).WithMessage("All storey heights must be greater than 0");
        }

        private bool HaveValidStoreyHeights(Dictionary<int, double> storeyHeights)
        {
            return storeyHeights.Values.All(h => h > 0);
        }
    }
}