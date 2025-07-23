using AutoMapper;
using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.Collaboration;
using System.Text.Json;

namespace TeiasMongoAPI.Services.Mappings
{
    public class UiComponentMappingProfile : Profile
    {
        public UiComponentMappingProfile()
        {
            // Request to Domain
            CreateMap<UiComponentCreateDto, UiComponent>()
                .ForMember(dest => dest._ID, opt => opt.Ignore())
                .ForMember(dest => dest.Creator, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.ProgramId, opt => opt.Ignore())
                .ForMember(dest => dest.VersionId, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => "active"));

            CreateMap<UiComponentUpdateDto, UiComponent>()
                .ForMember(dest => dest.ProgramId, opt => opt.Ignore())
                .ForMember(dest => dest.VersionId, opt => opt.Ignore())
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

            // Domain to Response
            CreateMap<UiComponent, UiComponentDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.Configuration, opt => opt.MapFrom(src => ConvertBsonObjectToStandardObject(src.Configuration)))
                .ForMember(dest => dest.Schema, opt => opt.MapFrom(src => ConvertBsonObjectToStandardObject(src.Schema)))
                .ForMember(dest => dest.ProgramId, opt => opt.MapFrom(src => src.ProgramId.ToString()))
                .ForMember(dest => dest.VersionId, opt => opt.MapFrom(src => src.VersionId.ToString()));

            CreateMap<UiComponent, UiComponentListDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.ProgramId, opt => opt.MapFrom(src => src.ProgramId.ToString()))
                .ForMember(dest => dest.VersionId, opt => opt.MapFrom(src => src.VersionId.ToString()))
                .ForMember(dest => dest.CreatorName, opt => opt.Ignore()) // Resolved in service
                .ForMember(dest => dest.ProgramName, opt => opt.Ignore()) // Resolved in service
                .ForMember(dest => dest.VersionNumber, opt => opt.Ignore()) // Resolved in service
                .ForMember(dest => dest.UsageCount, opt => opt.Ignore()); // Calculated in service

            CreateMap<UiComponent, UiComponentDetailDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.ProgramId, opt => opt.MapFrom(src => src.ProgramId.ToString()))
                .ForMember(dest => dest.VersionId, opt => opt.MapFrom(src => src.VersionId.ToString()))
                .ForMember(dest => dest.Configuration, opt => opt.MapFrom(src => ConvertBsonObjectToStandardObject(src.Configuration)))
                .ForMember(dest => dest.Schema, opt => opt.MapFrom(src => ConvertBsonObjectToStandardObject(src.Schema)))
                .ForMember(dest => dest.CreatorName, opt => opt.Ignore()) // Resolved in service
                .ForMember(dest => dest.ProgramName, opt => opt.Ignore()) // Resolved in service
                .ForMember(dest => dest.VersionNumber, opt => opt.Ignore()) // Resolved in service
                .ForMember(dest => dest.Assets, opt => opt.Ignore()) // Loaded from storage in service
                .ForMember(dest => dest.BundleInfo, opt => opt.Ignore()) // Populated in service
                .ForMember(dest => dest.Stats, opt => opt.Ignore()) // Calculated in service
                .ForMember(dest => dest.Usage, opt => opt.Ignore()); // Loaded from mappings in service

            // Asset and Bundle mappings
            CreateMap<UiComponentAssetUploadDto, UiComponentAssetDto>()
                .ForMember(dest => dest.Size, opt => opt.MapFrom(src => src.Content.Length))
                .ForMember(dest => dest.LastModified, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.Url, opt => opt.Ignore()); // Generated in service

            CreateMap<UiComponentBundleUploadDto, UiComponentBundleDto>()
                .ForMember(dest => dest.Id, opt => opt.Ignore()) // Generated in service
                .ForMember(dest => dest.ComponentId, opt => opt.Ignore()) // Set in service
                .ForMember(dest => dest.Assets, opt => opt.MapFrom(src => src.Assets))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.TotalSize, opt => opt.MapFrom(src => src.Assets.Sum(a => a.Content.Length)));

            // Configuration and Schema mappings
            CreateMap<UiComponentConfigUpdateDto, UiComponent>()
                .ForMember(dest => dest.Configuration, opt => opt.MapFrom(src => ParseJsonToDictionary(src.Configuration)))
                .ForMember(dest => dest._ID, opt => opt.Ignore())
                .ForMember(dest => dest.Name, opt => opt.Ignore())
                .ForMember(dest => dest.Description, opt => opt.Ignore())
                .ForMember(dest => dest.Type, opt => opt.Ignore())
                .ForMember(dest => dest.Creator, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.ProgramId, opt => opt.Ignore())
                .ForMember(dest => dest.VersionId, opt => opt.Ignore())
                .ForMember(dest => dest.Schema, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.Ignore())
                .ForMember(dest => dest.Tags, opt => opt.Ignore());

            CreateMap<UiComponentSchemaUpdateDto, UiComponent>()
                .ForMember(dest => dest.Schema, opt => opt.MapFrom(src => ParseJsonToDictionary(src.Schema)))
                .ForMember(dest => dest._ID, opt => opt.Ignore())
                .ForMember(dest => dest.Name, opt => opt.Ignore())
                .ForMember(dest => dest.Description, opt => opt.Ignore())
                .ForMember(dest => dest.Type, opt => opt.Ignore())
                .ForMember(dest => dest.Creator, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.ProgramId, opt => opt.Ignore())
                .ForMember(dest => dest.VersionId, opt => opt.Ignore())
                .ForMember(dest => dest.Configuration, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.Ignore())
                .ForMember(dest => dest.Tags, opt => opt.Ignore());

            // Mapping between programs and components
            CreateMap<UiComponentMappingDto, ProgramComponentMappingDto>()
                .ForMember(dest => dest.Id, opt => opt.Ignore()) // Generated in service
                .ForMember(dest => dest.ProgramId, opt => opt.Ignore()) // Set in service
                .ForMember(dest => dest.VersionId, opt => opt.Ignore()) // Set in service
                .ForMember(dest => dest.ComponentName, opt => opt.Ignore()) // Resolved in service
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow));

            // Statistics and usage mappings
            CreateMap<List<UiComponent>, UiComponentStatsDto>()
                .ConvertUsing(src => new UiComponentStatsDto
                {
                    TotalUsage = 0, // This would be calculated from actual usage data
                    ActiveUsage = src.Count(c => c.Status == "active"),
                    LastUsed = src.Where(c => c.CreatedAt > DateTime.MinValue).Max(c => c.CreatedAt),
                    AverageRating = 0, // This would come from rating data
                    RatingCount = 0,
                    TotalDownloads = 0 // This would be tracked separately
                });

            // Validation mappings
            CreateMap<UiComponent, UiComponentValidationResult>()
                .ConvertUsing(src => new UiComponentValidationResult
                {
                    IsValid = ValidateComponent(src),
                    Errors = GetValidationErrors(src),
                    Warnings = GetValidationWarnings(src),
                    Suggestions = GetValidationSuggestions(src)
                });

            // Search and compatibility mappings
            CreateMap<UiComponentCompatibilitySearchDto, UiComponentRecommendationDto>()
                .ConvertUsing(src => new UiComponentRecommendationDto
                {
                    ComponentId = string.Empty,
                    ComponentName = string.Empty,
                    ComponentType = string.Empty,
                    ProgramId = string.Empty,
                    VersionId = string.Empty,
                    RecommendationReason = "Compatible with specified requirements",
                    CompatibilityScore = 0.0,
                    UsageCount = 0,
                    Rating = 0.0
                });

            // Bundle info mapping
            CreateMap<UiComponentBundleDto, UiComponentBundleInfoDto>()
                .ForMember(dest => dest.AssetUrls, opt => opt.MapFrom(src => src.Assets.Select(a => a.Url).ToList()))
                .ForMember(dest => dest.LastUpdated, opt => opt.MapFrom(src => src.CreatedAt));

            // Category mappings (if you have categories)
            CreateMap<string, UiComponentCategoryDto>()
                .ConvertUsing(categoryName => new UiComponentCategoryDto
                {
                    Name = categoryName,
                    Description = GetCategoryDescription(categoryName),
                    ComponentCount = 0, // Would be calculated in service
                    SubCategories = GetSubCategories(categoryName)
                });

            // Copy result mappings
            CreateMap<UiComponent, UiComponentCopyResultDto>()
                .ForMember(dest => dest.ComponentId, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.ComponentName, opt => opt.MapFrom(src => src.Name))
                .ForMember(dest => dest.Success, opt => opt.MapFrom(src => true))
                .ForMember(dest => dest.ErrorMessage, opt => opt.Ignore())
                .ForMember(dest => dest.AssetsCopied, opt => opt.Ignore()) // Set in service
                .ForMember(dest => dest.AssetCount, opt => opt.Ignore()); // Set in service
        }

        private bool ValidateComponent(UiComponent component)
        {
            // Updated validation logic for version-specific components
            return !string.IsNullOrEmpty(component.Name) &&
                   !string.IsNullOrEmpty(component.Type) &&
                   component.Configuration != null &&
                   component.Schema != null &&
                   component.ProgramId != ObjectId.Empty &&
                   component.VersionId != ObjectId.Empty;
        }

        // V V V ADD THIS NEW HELPER METHOD V V V
        // HELPER 2: Converts BsonDocument from the database into a standard Dictionary for DTOs
        private static Dictionary<string, object> ConvertBsonObjectToStandardObject(object obj)
        {
            if (obj == null)
                return new Dictionary<string, object>();

            if (obj is BsonDocument bsonDoc)
            {
                // The .ToDictionary() method recursively converts the BsonDocument
                // into a standard Dictionary<string, object>.
                return bsonDoc.ToDictionary();
            }

            if (obj is Dictionary<string, object> dict)
                return dict;

            // If it's any other type, try to convert it to dictionary or return empty
            try
            {
                if (obj is string jsonString && !string.IsNullOrWhiteSpace(jsonString))
                {
                    return ParseJsonToDictionary(jsonString);
                }
            }
            catch
            {
                // If conversion fails, return empty dictionary
            }

            return new Dictionary<string, object>();
        }

        private static object ConvertObject(object obj)
        {
            if (obj is JsonElement element)
            {
                switch (element.ValueKind)
                {
                    case JsonValueKind.Object:
                        var dict = new Dictionary<string, object>();
                        foreach (var prop in element.EnumerateObject())
                        {
                            dict[prop.Name] = ConvertObject(prop.Value);
                        }
                        return dict;

                    case JsonValueKind.Array:
                        var list = new List<object>();
                        foreach (var item in element.EnumerateArray())
                        {
                            list.Add(ConvertObject(item));
                        }
                        return list;

                    case JsonValueKind.String:
                        return element.GetString();

                    case JsonValueKind.Number:
                        if (element.TryGetInt64(out long l)) return l;
                        return element.GetDouble();

                    case JsonValueKind.True:
                        return true;

                    case JsonValueKind.False:
                        return false;

                    case JsonValueKind.Null:
                    case JsonValueKind.Undefined:
                        return null;
                }
            }
            // If it's not a JsonElement, return it as is.
            return obj;
        }

        private List<string> GetValidationErrors(UiComponent component)
        {
            var errors = new List<string>();

            if (string.IsNullOrEmpty(component.Name))
                errors.Add("Component name is required");

            if (string.IsNullOrEmpty(component.Type))
                errors.Add("Component type is required");

            if (component.Configuration == null)
                errors.Add("Component configuration is required");

            if (component.Schema == null)
                errors.Add("Component schema is required");

            if (component.ProgramId == ObjectId.Empty)
                errors.Add("Component must belong to a program");

            if (component.VersionId == ObjectId.Empty)
                errors.Add("Component must belong to a version");

            return errors;
        }

        private List<string> GetValidationWarnings(UiComponent component)
        {
            var warnings = new List<string>();

            if (string.IsNullOrEmpty(component.Description))
                warnings.Add("Component description is recommended for better discoverability");

            if (component.Tags.Count == 0)
                warnings.Add("Adding tags will improve component searchability");

            return warnings;
        }

        private List<UiComponentValidationSuggestionDto> GetValidationSuggestions(UiComponent component)
        {
            var suggestions = new List<UiComponentValidationSuggestionDto>();

            if (string.IsNullOrEmpty(component.Description))
            {
                suggestions.Add(new UiComponentValidationSuggestionDto
                {
                    Type = "description",
                    Message = "Consider adding a detailed description",
                    SuggestedValue = $"A {component.Type} component for {component.Name}"
                });
            }

            return suggestions;
        }

        private string GetCategoryDescription(string categoryName)
        {
            return categoryName.ToLowerInvariant() switch
            {
                "input_form" => "Components for user input and data collection",
                "visualization" => "Components for data display and visualization",
                "composite" => "Complex components combining multiple functionalities",
                "web_component" => "Reusable web components",
                "navigation" => "Components for navigation and routing",
                "layout" => "Components for page layout and structure",
                _ => $"Components in the {categoryName} category"
            };
        }

        private List<string> GetSubCategories(string categoryName)
        {
            return categoryName.ToLowerInvariant() switch
            {
                "input_form" => new List<string> { "text_input", "select", "checkbox", "radio", "file_upload" },
                "visualization" => new List<string> { "charts", "graphs", "tables", "maps", "dashboards" },
                "composite" => new List<string> { "forms", "wizards", "panels", "modals" },
                "web_component" => new List<string> { "angular_elements", "react_components", "vue_components" },
                _ => new List<string>()
            };
        }

        private static Dictionary<string, object> ParseJsonToDictionary(string jsonString)
        {
            if (string.IsNullOrWhiteSpace(jsonString))
                return new Dictionary<string, object>();

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, object>>(jsonString) ?? new Dictionary<string, object>();
            }
            catch (Exception)
            {
                // If parsing fails, return empty Dictionary
                // The service layer will handle validation and provide proper error messages
                return new Dictionary<string, object>();
            }
        }
    }
}