# Structured Output Implementation with Gemini API

## Overview

This implementation replaces the previous approach of using system prompts to request JSON format responses with Gemini's native **structured output** feature using `responseSchema` and `responseMimeType`.

## Benefits

1. **Reliability**: Gemini guarantees the output will conform to the schema (no more parsing failures)
2. **Cleaner Prompts**: System prompts no longer need detailed JSON format instructions
3. **Type Safety**: Schema defines exact structure and required fields
4. **Better Performance**: Less tokens needed in prompts, more consistent responses

## Changes Made

### 1. Updated `ILLMClient` Interface
**File**: `TeiasMongoAPI.Services/Interfaces/ILLMClient.cs`

Added `responseSchema` parameter to `ChatCompletionAsync`:
```csharp
Task<LLMResponse> ChatCompletionAsync(
    string systemPrompt,
    List<LLMMessage> messages,
    double? temperature = null,
    int? maxTokens = null,
    object? responseSchema = null,  // ← NEW
    CancellationToken cancellationToken = default);
```

### 2. Updated `GeminiLLMClient` Implementation
**File**: `TeiasMongoAPI.Services/Services/Implementations/AI/GeminiLLMClient.cs`

Modified to support structured output:
- When `responseSchema` is provided, sets `responseMimeType: "application/json"`
- Passes the schema to Gemini API's `generationConfig.responseSchema`

### 3. Created Schema Builder
**File**: `TeiasMongoAPI.Services/Services/Implementations/AI/GeminiSchemaBuilder.cs`

New helper class that builds OpenAPI 3.0-compatible schemas for Gemini:

```csharp
public static class GeminiSchemaBuilder
{
    public static object BuildAIAssistantResponseSchema()
    {
        // Returns schema for:
        // {
        //   displayText: string (required)
        //   fileOperations: array (required)
        //   warnings: array (optional)
        // }
    }
}
```

**Schema Features**:
- Uses `propertyOrdering` to ensure consistent field order
- Defines `required` fields to force completion
- Supports `nullable` for optional fields
- Uses `enum` for operation types (CREATE, UPDATE, DELETE)

### 4. Updated `AIAssistantService`
**File**: `TeiasMongoAPI.Services/Services/Implementations/AI/AIAssistantService.cs`

**Changes**:
- Passes schema to LLM client: `responseSchema: GeminiSchemaBuilder.BuildAIAssistantResponseSchema()`
- Simplified system prompt - removed verbose JSON format instructions
- Updated parser to expect valid JSON directly (no regex extraction needed)
- Better error logging for structured output failures

**Before** (System Prompt):
```
RESPONSE FORMAT:
```json
{
  "displayText": "Your explanation here",
  "fileOperations": [...],
  ...
}
```

IMPORTANT RULES:
- Always respond with valid JSON in the format above
- Use relative file paths from project root
...
```

**After** (System Prompt):
```
# YOUR TASK
You are helping developers write and modify code. Your response will be structured automatically.

When responding:
1. Provide a clear explanation in 'displayText' of what you're doing
2. For code changes, include 'fileOperations' with the operations to apply
3. For questions without code changes, leave fileOperations empty
...
```

## How It Works

1. **Request Flow**:
   ```
   AIAssistantService.ConverseAsync()
     → Build system prompt (simplified, no JSON format)
     → Create schema with GeminiSchemaBuilder
     → Call LLMClient.ChatCompletionAsync(responseSchema: schema)
     → GeminiLLMClient adds responseMimeType + responseSchema to API request
   ```

2. **Gemini API Request**:
   ```json
   {
     "contents": [...],
     "generationConfig": {
       "temperature": 0.7,
       "maxOutputTokens": 8192,
       "responseMimeType": "application/json",
       "responseSchema": {
         "type": "object",
         "properties": {
           "displayText": { "type": "string" },
           "fileOperations": { "type": "array", ... }
         },
         "required": ["displayText", "fileOperations"]
       }
     }
   }
   ```

3. **Response Parsing**:
   - Gemini returns valid JSON conforming to schema
   - Direct deserialization (no regex extraction needed)
   - Fallback handling for edge cases

## Testing

Build Status: ✅ **SUCCESS** (0 errors, warnings only)

To test the implementation:

1. Send an AI conversation request
2. Verify response is valid JSON matching schema
3. Check logs for "Successfully parsed structured response" message
4. Ensure no "Response format was not structured as expected" warnings

## References

- [Gemini Structured Output Documentation](https://ai.google.dev/gemini-api/docs/structured-output)
- [OpenAPI 3.0 Schema Specification](https://spec.openapis.org/oas/v3.0.0#schema-object)
- [Gemini API Generation Config](https://ai.google.dev/gemini-api/docs/text-generation)

## Migration Notes

- **No API changes**: Frontend/clients don't need updates
- **Backward compatible**: Fallback handling for non-structured responses
- **Token savings**: Shorter system prompts = lower input token costs
- **Better reliability**: Schema enforcement prevents malformed responses
