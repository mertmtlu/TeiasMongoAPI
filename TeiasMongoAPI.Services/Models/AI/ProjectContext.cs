namespace TeiasMongoAPI.Services.Models.AI
{
    /// <summary>
    /// Contains gathered context about a project for AI processing
    /// </summary>
    public class ProjectContext
    {
        /// <summary>
        /// Project structure analysis results
        /// </summary>
        public required ProjectStructureAnalysis Structure { get; set; }

        /// <summary>
        /// Complete project index showing all symbols
        /// Allows AI to "see" entire codebase without reading all files
        /// </summary>
        public ProjectIndex? Index { get; set; }

        /// <summary>
        /// File contents that were read for context
        /// Key: file path, Value: file content
        /// </summary>
        public Dictionary<string, string> FileContents { get; set; } = new();

        /// <summary>
        /// User's intent classification
        /// </summary>
        public UserIntent? Intent { get; set; }

        /// <summary>
        /// Semantic search results from vector database
        /// Relevant code chunks found via semantic similarity
        /// </summary>
        public List<VectorSearchResult>? SemanticSearchResults { get; set; }

        /// <summary>
        /// Estimated total tokens used for this context
        /// </summary>
        public int EstimatedTokens { get; set; }
    }

    /// <summary>
    /// Results from analyzing project structure (from IExecutionService)
    /// </summary>
    public class ProjectStructureAnalysis
    {
        /// <summary>
        /// Programming language detected
        /// </summary>
        public string Language { get; set; } = string.Empty;

        /// <summary>
        /// Project type (e.g., "Web Application", "Console App", etc.)
        /// </summary>
        public string ProjectType { get; set; } = string.Empty;

        /// <summary>
        /// Entry point files
        /// </summary>
        public List<string> EntryPoints { get; set; } = new();

        /// <summary>
        /// All source files in the project
        /// </summary>
        public List<string> SourceFiles { get; set; } = new();

        /// <summary>
        /// Configuration files (text-readable: .json, .xml, .yaml, .md, etc.)
        /// </summary>
        public List<string> ConfigFiles { get; set; } = new();

        /// <summary>
        /// Binary and non-text-readable files (images, DLLs, executables, etc.)
        /// AI can see these exist but won't try to read their content
        /// </summary>
        public List<string> BinaryFiles { get; set; } = new();

        /// <summary>
        /// Dependencies/packages used
        /// </summary>
        public List<string> Dependencies { get; set; } = new();

        /// <summary>
        /// Additional metadata about the project
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Represents the classified intent of a user's prompt
    /// </summary>
    public class UserIntent
    {
        /// <summary>
        /// Type of intent
        /// </summary>
        public IntentType Type { get; set; }

        /// <summary>
        /// Files that should be read to fulfill this intent
        /// </summary>
        public List<string> RequiredFiles { get; set; } = new();

        /// <summary>
        /// Target file path (for create/update operations)
        /// </summary>
        public string? TargetFile { get; set; }

        /// <summary>
        /// Estimated complexity level
        /// </summary>
        public ComplexityLevel Complexity { get; set; }

        /// <summary>
        /// Confidence score (0.0 to 1.0)
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Additional context about the intent
        /// </summary>
        public string? Context { get; set; }
    }

    /// <summary>
    /// Types of user intents
    /// </summary>
    public enum IntentType
    {
        /// <summary>
        /// Create a new file
        /// </summary>
        FileCreation,

        /// <summary>
        /// Update existing file(s)
        /// </summary>
        FileUpdate,

        /// <summary>
        /// Delete file(s)
        /// </summary>
        FileDeletion,

        /// <summary>
        /// Ask a question/get explanation
        /// </summary>
        Question,

        /// <summary>
        /// Fix a bug or error
        /// </summary>
        BugFix,

        /// <summary>
        /// Refactor code
        /// </summary>
        Refactoring,

        /// <summary>
        /// Add a new feature
        /// </summary>
        FeatureAddition,

        /// <summary>
        /// General code review
        /// </summary>
        CodeReview,

        /// <summary>
        /// Other/unclear intent
        /// </summary>
        Other
    }

    /// <summary>
    /// Complexity levels for intents
    /// </summary>
    public enum ComplexityLevel
    {
        /// <summary>
        /// Low complexity - single file, simple change
        /// </summary>
        Low,

        /// <summary>
        /// Medium complexity - multiple files, moderate changes
        /// </summary>
        Medium,

        /// <summary>
        /// High complexity - many files, complex changes
        /// </summary>
        High
    }
}
