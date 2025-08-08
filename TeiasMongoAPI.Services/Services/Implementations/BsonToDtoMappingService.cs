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
                // Handle different parameter types
                switch (parameters)
                {
                    case BsonDocument bsonDoc:
                        dto.Parameters = ConvertBsonToJsonSafe(bsonDoc);
                        break;
                        
                    case Dictionary<string, object> dict:
                        dto.Parameters = ConvertDictionaryToJsonSafe(dict);
                        break;
                        
                    case string jsonString when !string.IsNullOrEmpty(jsonString):
                        dto.Parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonString) ?? new();
                        break;
                        
                    default:
                        _logger.LogWarning("Unknown parameter type: {ParameterType}", parameters?.GetType().Name ?? "null");
                        dto.Parameters = new Dictionary<string, object>();
                        break;
                }

                // Extract input files if present
                if (dto.Parameters.ContainsKey("inputFiles"))
                {
                    dto.InputFiles = ExtractInputFiles(dto.Parameters["inputFiles"]);
                }
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
    }
}