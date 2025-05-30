using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Swagger;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Data.Repositories;
using TeiasMongoAPI.Services.Services.Implementations;

namespace TeiasMongoAPI.API.Controllers
{
    // Method 1: Add an endpoint to export schemas programmatically
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentationController : ControllerBase
    {
        private readonly IServiceProvider _serviceProvider;

        public DocumentationController(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Get all API schemas and endpoints
        /// </summary>
        [HttpGet("schemas")]
        public async Task<ActionResult> GetAllSchemas()
        {
            var swaggerProvider = _serviceProvider.GetRequiredService<ISwaggerProvider>();
            var swagger = swaggerProvider.GetSwagger("v1");

            var documentation = new
            {
                Info = swagger.Info,
                Servers = swagger.Servers,
                Paths = swagger.Paths.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Operations.ToDictionary(
                        op => op.Key.ToString(),
                        op => new
                        {
                            Summary = op.Value.Summary,
                            Description = op.Value.Description,
                            Parameters = op.Value.Parameters?.Select(p => new
                            {
                                Name = p.Name,
                                In = p.In?.ToString(),
                                Required = p.Required,
                                Schema = p.Schema,
                                Description = p.Description
                            }),
                            RequestBody = op.Value.RequestBody != null ? new
                            {
                                Description = op.Value.RequestBody.Description,
                                Required = op.Value.RequestBody.Required,
                                Content = op.Value.RequestBody.Content?.ToDictionary(
                                    c => c.Key,
                                    c => new { Schema = c.Value.Schema }
                                )
                            } : null,
                            Responses = op.Value.Responses?.ToDictionary(
                                r => r.Key,
                                r => new
                                {
                                    Description = r.Value.Description,
                                    Content = r.Value.Content?.ToDictionary(
                                        c => c.Key,
                                        c => new { Schema = c.Value.Schema }
                                    )
                                }
                            )
                        }
                    )
                ),
                Components = new
                {
                    Schemas = swagger.Components?.Schemas?.ToDictionary(
                        s => s.Key,
                        s => new
                        {
                            Type = s.Value.Type,
                            Properties = s.Value.Properties?.ToDictionary(
                                p => p.Key,
                                p => new
                                {
                                    Type = p.Value.Type,
                                    Format = p.Value.Format,
                                    Description = p.Value.Description,
                                    Required = s.Value.Required?.Contains(p.Key) ?? false,
                                    Enum = p.Value.Enum?.Select(e => e.ToString()),
                                    Example = p.Value.Example?.ToString()
                                }
                            ),
                            Required = s.Value.Required,
                            Description = s.Value.Description,
                            Enum = s.Value.Enum?.Select(e => e.ToString()),
                            Example = s.Value.Example?.ToString()
                        }
                    )
                }
            };

            return Ok(documentation);
        }

        /// <summary>
        /// Get enum mappings
        /// </summary>
        [HttpGet("enums")]
        public ActionResult GetEnumMappings()
        {
            var enumMappings = new Dictionary<string, Dictionary<int, string>>();

            // Get all enums from your assemblies
            var assemblies = new[]
            {
            typeof(Program).Assembly, // API assembly
            typeof(UserRoles).Assembly, // Core assembly
            typeof(BuildingService).Assembly,
            typeof(UnitOfWork).Assembly,
            // Add other assemblies as needed
        };

            foreach (var assembly in assemblies)
            {
                var enumTypes = assembly.GetTypes().Where(t => t.IsEnum);

                foreach (var enumType in enumTypes)
                {
                    var enumMapping = new Dictionary<int, string>();
                    var enumValues = Enum.GetValues(enumType);
                    var enumNames = Enum.GetNames(enumType);

                    for (int i = 0; i < enumValues.Length; i++)
                    {
                        var numericValue = Convert.ToInt32(enumValues.GetValue(i));
                        enumMapping[numericValue] = enumNames[i];
                    }

                    enumMappings[enumType.Name] = enumMapping;
                }
            }

            // Add your constants as pseudo-enums
            enumMappings["UserRoles"] = new Dictionary<int, string>
        {
            { 0, "Admin" },
            { 1, "Manager" },
            { 2, "Engineer" },
            { 3, "Viewer" },
            { 4, "Auditor" }
        };

            return Ok(enumMappings);
        }
    }
}
