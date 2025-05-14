using FluentValidation;
using TeiasMongoAPI.Services.DTOs.Request.Common;
using TeiasMongoAPI.Services.DTOs.Request.Hazard;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;

namespace TeiasMongoAPI.Services.Validators.Common
{
    public class LocationRequestDtoValidator : AbstractValidator<LocationRequestDto>
    {
        public LocationRequestDtoValidator()
        {
            RuleFor(x => x.Latitude)
                .InclusiveBetween(-90, 90).WithMessage("Latitude must be between -90 and 90");

            RuleFor(x => x.Longitude)
                .InclusiveBetween(-180, 180).WithMessage("Longitude must be between -180 and 180");
        }
    }

    public class EarthquakeLevelDtoValidator : AbstractValidator<EarthquakeLevelDto>
    {
        public EarthquakeLevelDtoValidator()
        {
            RuleFor(x => x.PGA).GreaterThanOrEqualTo(0).WithMessage("PGA must be non-negative");
            RuleFor(x => x.PGV).GreaterThanOrEqualTo(0).WithMessage("PGV must be non-negative");
            RuleFor(x => x.Ss).GreaterThanOrEqualTo(0).WithMessage("Ss must be non-negative");
            RuleFor(x => x.S1).GreaterThanOrEqualTo(0).WithMessage("S1 must be non-negative");
            RuleFor(x => x.Sds).GreaterThanOrEqualTo(0).WithMessage("Sds must be non-negative");
            RuleFor(x => x.Sd1).GreaterThanOrEqualTo(0).WithMessage("Sd1 must be non-negative");
        }
    }

    public class SoilDtoValidator : AbstractValidator<SoilDto>
    {
        public SoilDtoValidator()
        {
            RuleFor(x => x.DrillHoleCount)
                .GreaterThanOrEqualTo(0).WithMessage("Drill hole count must be non-negative");

            RuleFor(x => x.DistanceToActiveFaultKm)
                .GreaterThanOrEqualTo(0).WithMessage("Distance to active fault must be non-negative");

            RuleFor(x => x.SoilVS30)
                .GreaterThan(0).WithMessage("Soil VS30 must be greater than 0");

            When(x => x.HasSoilStudyReport, () =>
            {
                RuleFor(x => x.SoilStudyReportDate)
                    .NotNull().WithMessage("Soil study report date is required when report exists")
                    .Must(BeValidDate).WithMessage("Soil study report date must not be in the future");
            });
        }

        private bool BeValidDate(DateTime? date)
        {
            if (!date.HasValue) return true;
            return date.Value <= DateTime.UtcNow;
        }
    }

    public class PaginationRequestDtoValidator : AbstractValidator<PaginationRequestDto>
    {
        public PaginationRequestDtoValidator()
        {
            RuleFor(x => x.PageNumber)
                .GreaterThan(0).WithMessage("Page number must be greater than 0");

            RuleFor(x => x.PageSize)
                .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100");

            When(x => x.Sorting != null, () =>
            {
                RuleFor(x => x.Sorting)
                    .SetValidator(new SortingRequestDtoValidator());
            });
        }
    }

    public class SortingRequestDtoValidator : AbstractValidator<SortingRequestDto>
    {
        private readonly string[] allowedSortFields = { "Id", "Name", "CreatedDate", "UpdatedDate" };

        public SortingRequestDtoValidator()
        {
            RuleFor(x => x.Field)
                .NotEmpty().WithMessage("Sort field is required")
                .Must(BeAllowedSortField).WithMessage("Invalid sort field");
        }

        private bool BeAllowedSortField(string field)
        {
            return allowedSortFields.Contains(field, StringComparer.OrdinalIgnoreCase);
        }
    }
}