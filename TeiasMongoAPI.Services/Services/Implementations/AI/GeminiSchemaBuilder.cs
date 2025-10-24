namespace TeiasMongoAPI.Services.Services.Implementations.AI
{
    /// <summary>
    /// Helper class to build Gemini-compatible JSON schemas for structured output
    /// Based on OpenAPI 3.0 Schema specification subset
    /// </summary>
    public static class GeminiSchemaBuilder
    {
        /// <summary>
        /// Creates the response schema for AI Assistant output
        /// </summary>
        public static object BuildAIAssistantResponseSchema()
        {
            return new
            {
                type = "object",
                properties = new
                {
                    displayText = new
                    {
                        type = "string",
                        description = "Conversational explanation of what the AI is doing or answering"
                    },
                    fileOperations = new
                    {
                        type = "array",
                        description = "Array of file operations to apply",
                        items = new
                        {
                            type = "object",
                            properties = new
                            {
                                operationType = new
                                {
                                    type = "string",
                                    @enum = new[] { "CREATE", "UPDATE", "DELETE" },
                                    description = "Type of file operation"
                                },
                                filePath = new
                                {
                                    type = "string",
                                    description = "Relative path to the file from project root"
                                },
                                content = new
                                {
                                    type = "string",
                                    description = "Full file content for CREATE/UPDATE operations, null for DELETE",
                                    nullable = true
                                },
                                description = new
                                {
                                    type = "string",
                                    description = "Description of what this operation does"
                                },
                                focusLine = new
                                {
                                    type = "integer",
                                    description = "Optional line number to focus after operation",
                                    nullable = true
                                }
                            },
                            required = new[] { "operationType", "filePath", "description" },
                            propertyOrdering = new[] { "operationType", "filePath", "content", "description", "focusLine" }
                        }
                    },
                    warnings = new
                    {
                        type = "array",
                        description = "Optional warnings or notices for the user",
                        items = new
                        {
                            type = "string"
                        }
                    }
                },
                required = new[] { "displayText", "fileOperations" },
                propertyOrdering = new[] { "displayText", "fileOperations", "warnings" }
            };
        }

        /// <summary>
        /// Creates a simple string response schema
        /// </summary>
        public static object BuildSimpleTextSchema()
        {
            return new
            {
                type = "object",
                properties = new
                {
                    response = new
                    {
                        type = "string",
                        description = "The text response"
                    }
                },
                required = new[] { "response" }
            };
        }

        /// <summary>
        /// Creates the schema for AI-powered intent classification
        /// Classifies user intent including type, scope, and relevant files
        /// </summary>
        public static object BuildIntentClassificationSchema()
        {
            return new
            {
                type = "object",
                properties = new
                {
                    intentType = new
                    {
                        type = "string",
                        @enum = new[]
                        {
                            "FileCreation",
                            "FileUpdate",
                            "FileDeletion",
                            "Question",
                            "BugFix",
                            "Refactoring",
                            "FeatureAddition",
                            "CodeReview",
                            "Other"
                        },
                        description = "The type of intent the user has"
                    },
                    fileScope = new
                    {
                        type = "string",
                        @enum = new[] { "Specific", "Related", "AllFiles", "Pattern" },
                        description = @"Scope of files the user wants to work with:
- Specific: User mentions exact file(s) like 'update Login.cs'
- Related: User wants files related to a concept like 'authentication system'
- AllFiles: User wants entire codebase like 'all files', 'everything', 'whole project'
- Pattern: User wants files matching a pattern like 'all .cs files', 'test files'"
                    },
                    relatedConcept = new
                    {
                        type = "string",
                        description = "If scope is 'Related', extract the key concept/feature/system from the prompt. e.g., 'authentication', 'payment processing', 'user management'",
                        nullable = true
                    },
                    mentionedFiles = new
                    {
                        type = "array",
                        description = "List of explicitly mentioned filenames, patterns, or paths in the user prompt",
                        items = new
                        {
                            type = "string"
                        }
                    },
                    scopeReasoning = new
                    {
                        type = "string",
                        description = "Brief explanation of why you classified this scope (for debugging and transparency)"
                    },
                    complexity = new
                    {
                        type = "string",
                        @enum = new[] { "Low", "Medium", "High" },
                        description = "Estimated complexity: Low (single file, simple), Medium (multiple files), High (many files, complex)"
                    },
                    confidence = new
                    {
                        type = "number",
                        description = "Confidence score between 0.0 and 1.0"
                    }
                },
                required = new[] { "intentType", "fileScope", "scopeReasoning", "complexity", "confidence" },
                propertyOrdering = new[] { "intentType", "fileScope", "relatedConcept", "mentionedFiles", "scopeReasoning", "complexity", "confidence" }
            };
        }
    }
}
