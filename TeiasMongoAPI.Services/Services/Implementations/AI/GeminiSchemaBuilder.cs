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
    }
}
