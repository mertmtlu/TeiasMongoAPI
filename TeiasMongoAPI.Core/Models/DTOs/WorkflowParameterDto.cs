using System.Text.Json.Serialization;

namespace TeiasMongoAPI.Core.Models.DTOs
{
    /// <summary>
    /// DTO for workflow parameters - JSON serialization safe
    /// </summary>
    public class WorkflowParameterDto
    {
        [JsonPropertyName("parameterId")]
        public string ParameterId { get; set; } = string.Empty;
        
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
        
        [JsonPropertyName("value")]
        public object? Value { get; set; }
        
        [JsonPropertyName("metadata")]
        public Dictionary<string, object> Metadata { get; set; } = new();
        
        [JsonPropertyName("inputFiles")]
        public List<InputFileDto>? InputFiles { get; set; }
    }
    
    public class InputFileDto
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty; // Base64 encoded
        
        [JsonPropertyName("contentType")]
        public string ContentType { get; set; } = string.Empty;
        
        [JsonPropertyName("size")]
        public long Size { get; set; }
    }

    public class ProjectExecutionContextDto
    {
        [JsonPropertyName("executionId")]
        public string ExecutionId { get; set; } = string.Empty;
        
        [JsonPropertyName("projectDirectory")]
        public string ProjectDirectory { get; set; } = string.Empty;
        
        [JsonPropertyName("parameters")]
        public Dictionary<string, object> Parameters { get; set; } = new();
        
        [JsonPropertyName("inputFiles")]
        public List<InputFileDto> InputFiles { get; set; } = new();
    }
}