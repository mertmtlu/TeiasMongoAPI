namespace TeiasMongoAPI.Services.Models.AI
{
    /// <summary>
    /// Complete index of a project showing all symbols without implementations
    /// Lightweight representation that lets AI "see" entire codebase structure
    /// </summary>
    public class ProjectIndex
    {
        /// <summary>
        /// Program ID this index belongs to
        /// </summary>
        public required string ProgramId { get; set; }

        /// <summary>
        /// Version ID this index belongs to
        /// </summary>
        public required string VersionId { get; set; }

        /// <summary>
        /// Programming language
        /// </summary>
        public required string Language { get; set; }

        /// <summary>
        /// All files in the project with their symbols
        /// Key: file path, Value: list of symbols in that file
        /// </summary>
        public Dictionary<string, List<CodeSymbol>> FileSymbols { get; set; } = new();

        /// <summary>
        /// File tree structure for quick navigation
        /// </summary>
        public List<string> AllFiles { get; set; } = new();

        /// <summary>
        /// Estimated tokens this index would consume in a prompt
        /// </summary>
        public int EstimatedTokens { get; set; }

        /// <summary>
        /// When this index was generated
        /// </summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Total number of symbols indexed
        /// </summary>
        public int TotalSymbols { get; set; }
    }

    /// <summary>
    /// Represents a code symbol (class, function, interface, etc.)
    /// Contains signature but NOT implementation
    /// </summary>
    public class CodeSymbol
    {
        /// <summary>
        /// Type of symbol
        /// </summary>
        public required SymbolType Type { get; set; }

        /// <summary>
        /// Name of the symbol
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// Full signature (e.g., "public async Task<string> GetUserAsync(string id)")
        /// </summary>
        public required string Signature { get; set; }

        /// <summary>
        /// Parent symbol (if nested, e.g., method inside a class)
        /// </summary>
        public string? Parent { get; set; }

        /// <summary>
        /// Line number where this symbol is defined
        /// </summary>
        public int LineNumber { get; set; }

        /// <summary>
        /// Documentation comment if available
        /// </summary>
        public string? Documentation { get; set; }

        /// <summary>
        /// Visibility/access modifier (public, private, etc.)
        /// </summary>
        public string? Visibility { get; set; }

        /// <summary>
        /// Return type (for methods/functions)
        /// </summary>
        public string? ReturnType { get; set; }

        /// <summary>
        /// Parameters (for methods/functions)
        /// </summary>
        public List<string> Parameters { get; set; } = new();

        /// <summary>
        /// Decorators/Attributes applied to this symbol
        /// </summary>
        public List<string> Decorators { get; set; } = new();
    }

    /// <summary>
    /// Types of code symbols
    /// </summary>
    public enum SymbolType
    {
        /// <summary>
        /// Class definition
        /// </summary>
        Class,

        /// <summary>
        /// Interface definition
        /// </summary>
        Interface,

        /// <summary>
        /// Method/Function
        /// </summary>
        Method,

        /// <summary>
        /// Property/Field
        /// </summary>
        Property,

        /// <summary>
        /// Enum definition
        /// </summary>
        Enum,

        /// <summary>
        /// Struct definition
        /// </summary>
        Struct,

        /// <summary>
        /// Namespace/Module
        /// </summary>
        Namespace,

        /// <summary>
        /// Constructor
        /// </summary>
        Constructor,

        /// <summary>
        /// Delegate/Function pointer
        /// </summary>
        Delegate,

        /// <summary>
        /// Constant
        /// </summary>
        Constant,

        /// <summary>
        /// Variable
        /// </summary>
        Variable,

        /// <summary>
        /// Other/Unknown
        /// </summary>
        Other
    }
}
