using FluentValidation;
using TeiasMongoAPI.Services.DTOs.Request.RemoteApp;

namespace TeiasMongoAPI.Services.Validators
{
    public class RemoteAppCreateDtoValidator : AbstractValidator<RemoteAppCreateDto>
    {
        public RemoteAppCreateDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty()
                .WithMessage("Remote app name is required.")
                .Length(1, 200)
                .WithMessage("Remote app name must be between 1 and 200 characters.")
                .Matches(@"^[a-zA-Z0-9\s\-_.()]+$")
                .WithMessage("Remote app name can only contain letters, numbers, spaces, hyphens, underscores, dots and parentheses.");

            RuleFor(x => x.Description)
                .MaximumLength(1000)
                .WithMessage("Description cannot exceed 1000 characters.");

            RuleFor(x => x.Url)
                .NotEmpty()
                .WithMessage("URL is required.")
                .MaximumLength(2000)
                .WithMessage("URL cannot exceed 2000 characters.")
                .Must(BeValidUrl)
                .WithMessage("Please provide a valid URL (must start with http:// or https://).");

            RuleFor(x => x.AssignedUserIds)
                .NotNull()
                .WithMessage("Assigned user IDs list cannot be null.")
                .Must(HaveValidObjectIds)
                .WithMessage("All assigned user IDs must be valid ObjectId format.")
                .When(x => x.AssignedUserIds != null && x.AssignedUserIds.Any());
        }

        private bool BeValidUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            return Uri.TryCreate(url, UriKind.Absolute, out var uriResult) &&
                   (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }

        private bool HaveValidObjectIds(IList<string> userIds)
        {
            if (userIds == null || !userIds.Any())
                return true;

            return userIds.All(id => !string.IsNullOrWhiteSpace(id) && MongoDB.Bson.ObjectId.TryParse(id, out _));
        }
    }

    public class RemoteAppUpdateDtoValidator : AbstractValidator<RemoteAppUpdateDto>
    {
        public RemoteAppUpdateDtoValidator()
        {
            RuleFor(x => x.Name)
                .Length(1, 200)
                .WithMessage("Remote app name must be between 1 and 200 characters.")
                .Matches(@"^[a-zA-Z0-9\s\-_.()]+$")
                .WithMessage("Remote app name can only contain letters, numbers, spaces, hyphens, underscores, dots and parentheses.")
                .When(x => !string.IsNullOrEmpty(x.Name));

            RuleFor(x => x.Description)
                .MaximumLength(1000)
                .WithMessage("Description cannot exceed 1000 characters.")
                .When(x => x.Description != null);

            RuleFor(x => x.Url)
                .MaximumLength(2000)
                .WithMessage("URL cannot exceed 2000 characters.")
                .Must(BeValidUrl)
                .WithMessage("Please provide a valid URL (must start with http:// or https://).")
                .When(x => !string.IsNullOrEmpty(x.Url));

            RuleFor(x => x.AssignedUserIds)
                .Must(HaveValidObjectIds)
                .WithMessage("All assigned user IDs must be valid ObjectId format.")
                .When(x => x.AssignedUserIds != null && x.AssignedUserIds.Any());
        }

        private bool BeValidUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            return Uri.TryCreate(url, UriKind.Absolute, out var uriResult) &&
                   (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }

        private bool HaveValidObjectIds(IList<string> userIds)
        {
            if (userIds == null || !userIds.Any())
                return true;

            return userIds.All(id => !string.IsNullOrWhiteSpace(id) && MongoDB.Bson.ObjectId.TryParse(id, out _));
        }
    }

    public class RemoteAppUserAssignmentDtoValidator : AbstractValidator<RemoteAppUserAssignmentDto>
    {
        public RemoteAppUserAssignmentDtoValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty()
                .WithMessage("User ID is required.")
                .Must(BeValidObjectId)
                .WithMessage("User ID must be a valid ObjectId format.");
        }

        private bool BeValidObjectId(string id)
        {
            return !string.IsNullOrWhiteSpace(id) && MongoDB.Bson.ObjectId.TryParse(id, out _);
        }
    }
}