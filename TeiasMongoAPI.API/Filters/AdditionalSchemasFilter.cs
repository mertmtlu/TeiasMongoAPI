using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using TeiasMongoAPI.Services.DTOs.Request.Collaboration;

namespace TeiasMongoAPI.API.Filters
{
    /// <summary>
    /// Schema filter to ensure additional DTOs are included in the OpenAPI schema
    /// even when they are not directly used in controller endpoints.
    /// This is necessary for NSwag to generate TypeScript interfaces for these types.
    /// </summary>
    public class AdditionalSchemasFilter : ISchemaFilter
    {
        private static readonly Type[] AdditionalTypes = new[]
        {
            typeof(NamedPointDto),
            typeof(FileDataDto)
        };

        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (context.Type == typeof(IFormFile))
            {
                schema.Type = "string";
                schema.Format = "binary";
            }

            // Force registration of additional types by referencing them
            foreach (var type in AdditionalTypes)
            {
                if (!context.SchemaRepository.Schemas.ContainsKey(type.Name))
                {
                    context.SchemaGenerator.GenerateSchema(type, context.SchemaRepository);
                }
            }
        }
    }
}