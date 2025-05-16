using FluentValidation;
using MongoDB.Bson;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Services.DTOs.Request.Region;

namespace TeiasMongoAPI.Services.Validators.Region
{
    public class RegionCreateDtoValidator : AbstractValidator<RegionCreateDto>
    {
        private readonly IUnitOfWork _unitOfWork;

        public RegionCreateDtoValidator(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;

            RuleFor(x => x.ClientId)
                .NotEmpty().WithMessage("Client ID is required")
                .Must(BeValidObjectId).WithMessage("Invalid Client ID format")
                .MustAsync(ClientExists).WithMessage("Client not found");

            RuleFor(x => x.Id)
                .NotEmpty().WithMessage("Region ID is required")
                .GreaterThan(0).WithMessage("Region ID must be greater than 0")
                .MustAsync(BeUniqueRegionId).WithMessage("Region ID already exists");

            RuleFor(x => x.Headquarters)
                .NotEmpty().WithMessage("Headquarters is required")
                .MaximumLength(100).WithMessage("Headquarters cannot exceed 100 characters");

            RuleFor(x => x.Cities)
                .NotEmpty().WithMessage("At least one city is required")
                .Must(HaveUniqueCities).WithMessage("Cities must be unique")
                .ForEach(city => city
                    .NotEmpty().WithMessage("City name cannot be empty")
                    .MaximumLength(100).WithMessage("City name cannot exceed 100 characters"));
        }

        private bool BeValidObjectId(string objectId)
        {
            return ObjectId.TryParse(objectId, out _);
        }

        private async Task<bool> ClientExists(string clientId, CancellationToken cancellationToken)
        {
            if (!ObjectId.TryParse(clientId, out var objectId))
                return false;

            return await _unitOfWork.Clients.ExistsAsync(objectId, cancellationToken);
        }

        private async Task<bool> BeUniqueRegionId(int regionId, CancellationToken cancellationToken)
        {
            var existingRegion = await _unitOfWork.Regions.GetByNoAsync(regionId, cancellationToken);
            return existingRegion == null;
        }

        private bool HaveUniqueCities(List<string> cities)
        {
            return cities.Distinct(StringComparer.OrdinalIgnoreCase).Count() == cities.Count;
        }
    }
}

public class RegionUpdateDtoValidator : AbstractValidator<RegionUpdateDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public RegionUpdateDtoValidator(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;

        RuleFor(x => x.ClientId)
            .Must(BeValidObjectId).When(x => !string.IsNullOrEmpty(x.ClientId))
            .WithMessage("Invalid Client ID format")
            .MustAsync(ClientExists).When(x => !string.IsNullOrEmpty(x.ClientId))
            .WithMessage("Client not found");

        RuleFor(x => x.Id)
            .GreaterThan(0).When(x => x.Id.HasValue)
            .WithMessage("Region ID must be greater than 0");

        RuleFor(x => x.Headquarters)
            .MaximumLength(100).When(x => !string.IsNullOrEmpty(x.Headquarters))
            .WithMessage("Headquarters cannot exceed 100 characters");

        RuleFor(x => x.Cities)
            .Must(HaveUniqueCities).When(x => x.Cities != null && x.Cities.Any())
            .WithMessage("Cities must be unique");

        When(x => x.Cities != null, () =>
        {
            RuleForEach(x => x.Cities)
                .NotEmpty().WithMessage("City name cannot be empty")
                .MaximumLength(100).WithMessage("City name cannot exceed 100 characters");
        });
    }

    private bool BeValidObjectId(string objectId)
    {
        return ObjectId.TryParse(objectId, out _);
    }

    private async Task<bool> ClientExists(string clientId, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(clientId, out var objectId))
            return false;

        return await _unitOfWork.Clients.ExistsAsync(objectId, cancellationToken);
    }

    private bool HaveUniqueCities(List<string> cities)
    {
        return cities.Distinct(StringComparer.OrdinalIgnoreCase).Count() == cities.Count;
    }
}