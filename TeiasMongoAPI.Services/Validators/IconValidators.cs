using FluentValidation;
using MongoDB.Bson;
using System.Text.RegularExpressions;
using TeiasMongoAPI.Services.DTOs.Request.Icon;

namespace TeiasMongoAPI.Services.Validators
{
    public class IconCreateDtoValidator : AbstractValidator<IconCreateDto>
    {
        private static readonly string[] ValidFormats = { "png", "jpg", "jpeg", "gif", "svg", "webp", "ico" };
        private static readonly Regex Base64Regex = new(@"^[A-Za-z0-9+/]*={0,3}$", RegexOptions.Compiled);
        private const int MaxIconSizeBytes = 5 * 1024 * 1024; // 5MB

        public IconCreateDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Icon name is required")
                .Length(1, 100).WithMessage("Icon name must be between 1 and 100 characters");

            RuleFor(x => x.Description)
                .MaximumLength(500).WithMessage("Description cannot exceed 500 characters");

            RuleFor(x => x.IconData)
                .NotEmpty().WithMessage("Icon data is required")
                .Must(BeValidBase64).WithMessage("Icon data must be valid base64")
                .Must(BeWithinSizeLimit).WithMessage($"Icon size cannot exceed {MaxIconSizeBytes / 1024 / 1024}MB");

            RuleFor(x => x.Format)
                .NotEmpty().WithMessage("Icon format is required")
                .Must(BeValidFormat).WithMessage($"Format must be one of: {string.Join(", ", ValidFormats)}");

            RuleFor(x => x.EntityType)
                .IsInEnum().WithMessage("Valid entity type is required");

            RuleFor(x => x.EntityId)
                .NotEmpty().WithMessage("Entity ID is required")
                .Must(BeValidObjectId).WithMessage("Entity ID must be a valid ObjectId");
        }

        private static bool BeValidBase64(string base64)
        {
            if (string.IsNullOrEmpty(base64))
                return false;

            base64 = base64.Trim();
            return base64.Length % 4 == 0 && Base64Regex.IsMatch(base64);
        }

        private static bool BeWithinSizeLimit(string base64)
        {
            if (string.IsNullOrEmpty(base64))
                return false;

            var padding = 0;
            if (base64.EndsWith("==")) padding = 2;
            else if (base64.EndsWith("=")) padding = 1;

            var sizeInBytes = (base64.Length * 3 / 4) - padding;
            return sizeInBytes <= MaxIconSizeBytes;
        }

        private static bool BeValidFormat(string format)
        {
            return ValidFormats.Contains(format.ToLowerInvariant());
        }

        private static bool BeValidObjectId(string id)
        {
            return ObjectId.TryParse(id, out _);
        }
    }

    public class IconUpdateDtoValidator : AbstractValidator<IconUpdateDto>
    {
        private static readonly string[] ValidFormats = { "png", "jpg", "jpeg", "gif", "svg", "webp", "ico" };
        private static readonly Regex Base64Regex = new(@"^[A-Za-z0-9+/]*={0,3}$", RegexOptions.Compiled);
        private const int MaxIconSizeBytes = 5 * 1024 * 1024; // 5MB

        public IconUpdateDtoValidator()
        {
            RuleFor(x => x.Name)
                .Length(1, 100).WithMessage("Icon name must be between 1 and 100 characters")
                .When(x => !string.IsNullOrEmpty(x.Name));

            RuleFor(x => x.Description)
                .MaximumLength(500).WithMessage("Description cannot exceed 500 characters")
                .When(x => x.Description != null);

            RuleFor(x => x.IconData)
                .Must(BeValidBase64).WithMessage("Icon data must be valid base64")
                .Must(BeWithinSizeLimit).WithMessage($"Icon size cannot exceed {MaxIconSizeBytes / 1024 / 1024}MB")
                .When(x => !string.IsNullOrEmpty(x.IconData));

            RuleFor(x => x.Format)
                .Must(BeValidFormat).WithMessage($"Format must be one of: {string.Join(", ", ValidFormats)}")
                .When(x => !string.IsNullOrEmpty(x.Format));
        }

        private static bool BeValidBase64(string base64)
        {
            if (string.IsNullOrEmpty(base64))
                return true; // Allow null/empty for updates

            base64 = base64.Trim();
            return base64.Length % 4 == 0 && Base64Regex.IsMatch(base64);
        }

        private static bool BeWithinSizeLimit(string base64)
        {
            if (string.IsNullOrEmpty(base64))
                return true;

            var padding = 0;
            if (base64.EndsWith("==")) padding = 2;
            else if (base64.EndsWith("=")) padding = 1;

            var sizeInBytes = (base64.Length * 3 / 4) - padding;
            return sizeInBytes <= MaxIconSizeBytes;
        }

        private static bool BeValidFormat(string format)
        {
            return ValidFormats.Contains(format.ToLowerInvariant());
        }
    }

    public class IconBatchRequestDtoValidator : AbstractValidator<IconBatchRequestDto>
    {
        public IconBatchRequestDtoValidator()
        {
            RuleFor(x => x.IconIds)
                .NotEmpty().WithMessage("Icon IDs are required")
                .Must(ids => ids.Count() <= 50).WithMessage("Cannot request more than 50 icons at once")
                .Must(AllValidObjectIds).WithMessage("All icon IDs must be valid ObjectIds");
        }

        private static bool AllValidObjectIds(IEnumerable<string> ids)
        {
            return ids.All(id => ObjectId.TryParse(id, out _));
        }
    }

    public class IconEntityBatchRequestDtoValidator : AbstractValidator<IconEntityBatchRequestDto>
    {
        public IconEntityBatchRequestDtoValidator()
        {
            RuleFor(x => x.EntityType)
                .IsInEnum().WithMessage("Valid entity type is required");

            RuleFor(x => x.EntityIds)
                .NotEmpty().WithMessage("Entity IDs are required")
                .Must(ids => ids.Count() <= 50).WithMessage("Cannot request more than 50 entities at once")
                .Must(AllValidObjectIds).WithMessage("All entity IDs must be valid ObjectIds");
        }

        private static bool AllValidObjectIds(IEnumerable<string> ids)
        {
            return ids.All(id => ObjectId.TryParse(id, out _));
        }
    }

    public class IconValidationRequestDtoValidator : AbstractValidator<IconValidationRequestDto>
    {
        public IconValidationRequestDtoValidator()
        {
            RuleFor(x => x.EntityType)
                .IsInEnum().WithMessage("Valid entity type is required");

            RuleFor(x => x.EntityId)
                .NotEmpty().WithMessage("Entity ID is required")
                .Must(BeValidObjectId).WithMessage("Entity ID must be a valid ObjectId");

            RuleFor(x => x.ExcludeIconId)
                .Must(BeValidObjectId).WithMessage("Exclude icon ID must be a valid ObjectId")
                .When(x => !string.IsNullOrEmpty(x.ExcludeIconId));
        }

        private static bool BeValidObjectId(string id)
        {
            return ObjectId.TryParse(id, out _);
        }
    }
}