using MongoDB.Bson;
using MongoDB.Bson.IO;
using TeiasMongoAPI.Core.Models.DTOs;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace TeiasMongoAPI.Services.Services.Implementations
{
    public interface IBsonToDtoMappingService
    {
        Dictionary<string, object> ConvertBsonToJsonSafe(BsonDocument document);
        object ConvertBsonValueToJsonSafe(BsonValue value);
        ProjectExecutionContextDto MapToExecutionContextDto(object parameters, string executionId, string projectDirectory);
    }

    public class BsonToDtoMappingService : IBsonToDtoMappingService
    {
        private readonly ILogger<BsonToDtoMappingService> _logger;

        public BsonToDtoMappingService(ILogger<BsonToDtoMappingService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// CRITICAL FIX: Safely converts BsonDocument to JSON-serializable Dictionary
        /// </summary>
        public Dictionary<string, object> ConvertBsonToJsonSafe(BsonDocument document)
        {
            if (document == null) return new Dictionary<string, object>();

            var result = new Dictionary<string, object>();
            
            foreach (var element in document.Elements)
            {
                try
                {
                    result[element.Name] = ConvertBsonValueToJsonSafe(element.Value);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to convert BSON element {ElementName} of type {BsonType}", 
                        element.Name, element.Value.BsonType);
                    
                    // Fallback to string representation
                    result[element.Name] = element.Value.ToString() ?? string.Empty;
                }
            }
            
            return result;
        }

        /// <summary>
        /// CRITICAL FIX: Comprehensive BSON value conversion handling all types
        /// </summary>
        public object ConvertBsonValueToJsonSafe(BsonValue value)
        {
            if (value == null || value.IsBsonNull) return null!;

            return value.BsonType switch
            {
                BsonType.String => value.AsString,
                BsonType.Int32 => value.AsInt32,
                BsonType.Int64 => value.AsInt64,
                BsonType.Double => value.AsDouble,
                BsonType.Decimal128 => (double)value.AsDecimal128,
                BsonType.Boolean => value.AsBoolean,
                BsonType.DateTime => value.ToUniversalTime(),
                BsonType.ObjectId => value.AsObjectId.ToString(),
                BsonType.Document => ConvertBsonToJsonSafe(value.AsBsonDocument),
                BsonType.Array => value.AsBsonArray.Select(ConvertBsonValueToJsonSafe).ToList(),
                BsonType.Binary when value.AsBsonBinaryData.SubType == BsonBinarySubType.UuidStandard || value.AsBsonBinaryData.SubType == BsonBinarySubType.UuidLegacy => value.AsGuid.ToString(),
                BsonType.Binary => Convert.ToBase64String(value.AsBsonBinaryData.Bytes),
                BsonType.RegularExpression => value.AsRegex.ToString(),
                
                // Handle all other types with string fallback
                _ => value.ToString() ?? string.Empty
            };
        }

        /// <summary>
        /// CRITICAL FIX: Maps MongoDB parameters to JSON-safe DTO
        /// </summary>
        public ProjectExecutionContextDto MapToExecutionContextDto(object parameters, string executionId, string projectDirectory)
        {
            var dto = new ProjectExecutionContextDto
            {
                ExecutionId = executionId,
                ProjectDirectory = projectDirectory
            };

            try
            {
                // Log the actual parameter type for debugging
                _logger.LogDebug("Processing parameters of type: {ParameterType}, Value: {ParameterValue}",
                    parameters?.GetType().FullName ?? "null", parameters?.ToString() ?? "null");


                // Handle different parameter types
                switch (parameters)
                {
                    case BsonDocument bsonDoc:
                        _logger.LogDebug("Processing BsonDocument parameters");
                        dto.Parameters = ConvertBsonToJsonSafe(bsonDoc);
                        break;

                    case Dictionary<string, object> dict:
                        _logger.LogDebug("Processing Dictionary<string, object> parameters");
                        dto.Parameters = ConvertDictionaryToJsonSafe(dict);
                        break;

                    case string jsonString when !string.IsNullOrEmpty(jsonString):
                        _logger.LogDebug("Processing string JSON parameters");
                        dto.Parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonString) ?? new();
                        break;

                    case JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.Object:
                        _logger.LogDebug("Processing JsonElement parameters");
                        dto.Parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonElement.GetRawText()) ?? new();
                        break;

                    default:
                        _logger.LogWarning("Unknown parameter type: {ParameterType}, attempting JSON deserialization from string", parameters?.GetType().FullName ?? "null");

                        // Try to convert to string and then deserialize as JSON
                        var paramString = parameters?.ToString();
                        if (!string.IsNullOrEmpty(paramString))
                        {
                            try
                            {
                                dto.Parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(paramString) ?? new();
                                _logger.LogDebug("Successfully parsed unknown type as JSON string");
                            }
                            catch (JsonException jsonEx)
                            {
                                _logger.LogError(jsonEx, "Failed to parse unknown parameter type as JSON");
                                dto.Parameters = new Dictionary<string, object>();
                            }
                        }
                        else
                        {
                            dto.Parameters = new Dictionary<string, object>();
                        }
                        break;
                }

                // Extract input files from both old format (inputFiles array) and new format (direct parameters)
                if (dto.Parameters.ContainsKey("inputFiles"))
                {
                    dto.InputFiles = ExtractInputFiles(dto.Parameters["inputFiles"]);
                }

                // NEW: Extract files from direct parameter format (filename + content)
                var extractedFiles = ExtractFilesFromParameters(dto.Parameters);
                _logger.LogDebug("Extracted {FileCount} files from parameters for execution {ExecutionId}", extractedFiles.Count, executionId);
                dto.InputFiles.AddRange(extractedFiles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to map parameters to DTO for execution {ExecutionId}", executionId);
                dto.Parameters = new Dictionary<string, object>();
            }

            return dto;
        }

        private Dictionary<string, object> ConvertDictionaryToJsonSafe(Dictionary<string, object> dict)
        {
            var result = new Dictionary<string, object>();
            
            foreach (var kvp in dict)
            {
                try
                {
                    result[kvp.Key] = kvp.Value switch
                    {
                        BsonDocument bsonDoc => ConvertBsonToJsonSafe(bsonDoc),
                        BsonValue bsonValue => ConvertBsonValueToJsonSafe(bsonValue),
                        _ => kvp.Value
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to convert dictionary value for key {Key}", kvp.Key);
                    result[kvp.Key] = kvp.Value?.ToString() ?? string.Empty;
                }
            }
            
            return result;
        }

        private List<InputFileDto> ExtractInputFiles(object inputFilesObj)
        {
            try
            {
                var inputFiles = new List<InputFileDto>();
                
                // Handle different input file representations
                switch (inputFilesObj)
                {
                    case BsonArray bsonArray:
                        foreach (var item in bsonArray)
                        {
                            if (item.IsBsonDocument)
                            {
                                var fileDoc = item.AsBsonDocument;
                                inputFiles.Add(new InputFileDto
                                {
                                    Name = fileDoc.GetValue("name", "").AsString,
                                    Content = fileDoc.GetValue("content", "").AsString,
                                    ContentType = fileDoc.GetValue("contentType", "").AsString,
                                    Size = fileDoc.GetValue("size", 0).ToInt64()
                                });
                            }
                        }
                        break;
                        
                    case List<object> objectList:
                        foreach (var item in objectList)
                        {
                            if (item is Dictionary<string, object> fileDict)
                            {
                                inputFiles.Add(new InputFileDto
                                {
                                    Name = fileDict.GetValueOrDefault("name", "").ToString() ?? "",
                                    Content = fileDict.GetValueOrDefault("content", "").ToString() ?? "",
                                    ContentType = fileDict.GetValueOrDefault("contentType", "").ToString() ?? "",
                                    Size = Convert.ToInt64(fileDict.GetValueOrDefault("size", 0))
                                });
                            }
                        }
                        break;
                }
                
                return inputFiles;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract input files");
                return new List<InputFileDto>();
            }
        }

        private List<InputFileDto> ExtractFilesFromParameters(Dictionary<string, object> parameters)
        {
            try
            {
                var inputFiles = new List<InputFileDto>();
                
                foreach (var kvp in parameters)
                {
                    // Check if this parameter looks like a file object (has filename and content)
                    if (kvp.Value is Dictionary<string, object> fileDict)
                    {
                        if (fileDict.ContainsKey("filename") && fileDict.ContainsKey("content"))
                        {
                            var filename = fileDict.GetValueOrDefault("filename", "").ToString() ?? "";
                            var content = fileDict.GetValueOrDefault("content", "").ToString() ?? "";
                            var contentType = fileDict.GetValueOrDefault("contentType", "application/octet-stream").ToString() ?? "application/octet-stream";
                            var fileSize = Convert.ToInt64(fileDict.GetValueOrDefault("fileSize", 0));

                            if (!string.IsNullOrEmpty(filename) && !string.IsNullOrEmpty(content))
                            {
                                inputFiles.Add(new InputFileDto
                                {
                                    Name = filename,
                                    Content = content,
                                    ContentType = contentType,
                                    Size = fileSize
                                });

                                _logger.LogDebug("Extracted file from parameters: {FileName} ({Size} bytes)", filename, fileSize);
                            }
                        }
                    }
                    // Handle JSON elements that might contain file data
                    else if (kvp.Value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
                    {
                        try
                        {
                            var fileDictonary = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonElement.GetRawText());
                            if (fileDictonary?.ContainsKey("filename") == true && fileDictonary?.ContainsKey("content") == true)
                            {
                                var filename = fileDictonary.GetValueOrDefault("filename", "").ToString() ?? "";
                                var content = fileDictonary.GetValueOrDefault("content", "").ToString() ?? "";
                                var contentType = fileDictonary.GetValueOrDefault("contentType", "application/octet-stream").ToString() ?? "application/octet-stream";
                                var baseFileSize = fileDictonary.GetValueOrDefault("fileSize", 0).ToString();


                                var fileSize = Convert.ToInt64(baseFileSize);

                                if (!string.IsNullOrEmpty(filename) && !string.IsNullOrEmpty(content))
                                {
                                    inputFiles.Add(new InputFileDto
                                    {
                                        Name = filename,
                                        Content = content,
                                        ContentType = contentType,
                                        Size = fileSize
                                    });

                                    _logger.LogDebug("Extracted file from JSON parameters: {FileName} ({Size} bytes)", filename, fileSize);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Failed to parse JSON element as file data for parameter {ParameterName}", kvp.Key);
                        }
                    }
                }
                
                return inputFiles;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract files from parameters");
                return new List<InputFileDto>();
            }
        }
    }
}