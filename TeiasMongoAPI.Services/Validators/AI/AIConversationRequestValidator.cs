using FluentValidation;
using TeiasMongoAPI.Services.DTOs.Request.AI;

namespace TeiasMongoAPI.Services.Validators.AI
{
    /// <summary>
    /// Validator for AI conversation requests
    /// </summary>
    public class AIConversationRequestValidator : AbstractValidator<AIConversationRequestDto>
    {
        public AIConversationRequestValidator()
        {
            RuleFor(x => x.UserPrompt)
                .NotEmpty()
                .WithMessage("User prompt is required")
                .MaximumLength(4000)
                .WithMessage("Prompt must be 4000 characters or less");

            RuleFor(x => x.ProgramId)
                .NotEmpty()
                .WithMessage("Program ID is required")
                .Must(BeValidObjectId)
                .WithMessage("Invalid Program ID format");

            RuleFor(x => x.VersionId)
                .Must(BeValidObjectIdOrNull)
                .When(x => !string.IsNullOrEmpty(x.VersionId))
                .WithMessage("Invalid Version ID format");

            RuleFor(x => x.ConversationHistory)
                .NotNull()
                .WithMessage("Conversation history cannot be null");

            RuleFor(x => x.ConversationHistory)
                .Must(history => history.Count <= 50)
                .When(x => x.ConversationHistory != null)
                .WithMessage("Conversation history cannot exceed 50 messages");

            RuleFor(x => x.Preferences)
                .SetValidator(new AIPreferencesValidator()!)
                .When(x => x.Preferences != null);
        }

        private bool BeValidObjectId(string id)
        {
            return MongoDB.Bson.ObjectId.TryParse(id, out _);
        }

        private bool BeValidObjectIdOrNull(string? id)
        {
            if (string.IsNullOrEmpty(id)) return true;
            return MongoDB.Bson.ObjectId.TryParse(id, out _);
        }
    }

    /// <summary>
    /// Validator for AI preferences
    /// </summary>
    public class AIPreferencesValidator : AbstractValidator<AIPreferences>
    {
        public AIPreferencesValidator()
        {
            RuleFor(x => x.Verbosity)
                .Must(v => new[] { "concise", "normal", "detailed" }.Contains(v))
                .WithMessage("Verbosity must be 'concise', 'normal', or 'detailed'");

            RuleFor(x => x.ContextMode)
                .Must(c => new[] { "aggressive", "balanced", "comprehensive" }.Contains(c))
                .WithMessage("Context mode must be 'aggressive', 'balanced', or 'comprehensive'");

            RuleFor(x => x.MaxFileOperations)
                .GreaterThan(0)
                .WithMessage("Max file operations must be greater than 0")
                .LessThanOrEqualTo(20)
                .WithMessage("Max file operations cannot exceed 20");
        }
    }

    /// <summary>
    /// Validator for conversation messages
    /// </summary>
    public class ConversationMessageValidator : AbstractValidator<ConversationMessage>
    {
        public ConversationMessageValidator()
        {
            RuleFor(x => x.Role)
                .NotEmpty()
                .WithMessage("Message role is required")
                .Must(r => new[] { "user", "assistant" }.Contains(r))
                .WithMessage("Role must be 'user' or 'assistant'");

            RuleFor(x => x.Content)
                .NotEmpty()
                .WithMessage("Message content is required")
                .MaximumLength(10000)
                .WithMessage("Message content cannot exceed 10000 characters");
        }
    }

    /// <summary>
    /// Validator for file operations
    /// </summary>
    public class FileOperationValidator : AbstractValidator<FileOperationDto>
    {
        public FileOperationValidator()
        {
            RuleFor(x => x.FilePath)
                .NotEmpty()
                .WithMessage("File path is required")
                .Must(BeValidFilePath)
                .WithMessage("File path must not contain invalid characters or absolute paths");

            RuleFor(x => x.Content)
                .NotEmpty()
                .When(x => x.OperationType == FileOperationType.CREATE || x.OperationType == FileOperationType.UPDATE)
                .WithMessage("Content is required for CREATE and UPDATE operations");

            RuleFor(x => x.Content)
                .Null()
                .When(x => x.OperationType == FileOperationType.DELETE)
                .WithMessage("Content should be null for DELETE operations");
        }

        private bool BeValidFilePath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;

            // Disallow absolute paths
            if (Path.IsPathRooted(filePath)) return false;

            // Disallow path traversal
            if (filePath.Contains("..")) return false;

            // Check for invalid characters
            var invalidChars = Path.GetInvalidPathChars();
            if (filePath.Any(c => invalidChars.Contains(c))) return false;

            return true;
        }
    }
}
