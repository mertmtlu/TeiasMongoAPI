using FluentValidation;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Services.DTOs.Request.Auth;
using TeiasMongoAPI.Services.Security;

namespace TeiasMongoAPI.Services.Validators.Auth
{
    public class UserRegisterDtoValidator : AbstractValidator<UserRegisterDto>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPasswordHashingService _passwordHashingService;

        public UserRegisterDtoValidator(IUnitOfWork unitOfWork, IPasswordHashingService passwordHashingService)
        {
            _unitOfWork = unitOfWork;
            _passwordHashingService = passwordHashingService;

            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required")
                .EmailAddress().WithMessage("Invalid email format")
                .MustAsync(BeUniqueEmail).WithMessage("Email already exists");

            RuleFor(x => x.Username)
                .NotEmpty().WithMessage("Username is required")
                .Length(3, 50).WithMessage("Username must be between 3 and 50 characters")
                .Matches("^[a-zA-Z0-9_-]+$").WithMessage("Username can only contain letters, numbers, underscores, and hyphens")
                .MustAsync(BeUniqueUsername).WithMessage("Username already exists");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Password is required")
                .MinimumLength(6).WithMessage("Password must be at least 6 characters")
                .Must(BeComplexPassword).WithMessage(PasswordRequirements.GetPasswordPolicy());

            RuleFor(x => x.ConfirmPassword)
                .Equal(x => x.Password).WithMessage("Passwords do not match");

            RuleFor(x => x.FirstName)
                .MaximumLength(100).WithMessage("First name cannot exceed 100 characters")
                .Matches("^[a-zA-Z\\s-']+$").When(x => !string.IsNullOrEmpty(x.FirstName))
                .WithMessage("First name can only contain letters, spaces, hyphens, and apostrophes");

            RuleFor(x => x.LastName)
                .MaximumLength(100).WithMessage("Last name cannot exceed 100 characters")
                .Matches("^[a-zA-Z\\s-']+$").When(x => !string.IsNullOrEmpty(x.LastName))
                .WithMessage("Last name can only contain letters, spaces, hyphens, and apostrophes");
        }

        private async Task<bool> BeUniqueEmail(string email, CancellationToken cancellationToken)
        {
            return !await _unitOfWork.Users.EmailExistsAsync(email, cancellationToken);
        }

        private async Task<bool> BeUniqueUsername(string username, CancellationToken cancellationToken)
        {
            return !await _unitOfWork.Users.UsernameExistsAsync(username, cancellationToken);
        }

        private bool BeComplexPassword(string password)
        {
            return _passwordHashingService.IsPasswordComplex(password);
        }
    }
}