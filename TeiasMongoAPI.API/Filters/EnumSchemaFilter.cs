using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace TeiasMongoAPI.API.Filters
{
    public class EnumSchemaFilter : ISchemaFilter
    {
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (context.Type.IsEnum)
            {
                schema.Enum.Clear();
                var enumValues = new List<IOpenApiAny>();
                var enumNames = Enum.GetNames(context.Type);
                var enumNumericValues = Enum.GetValues(context.Type);

                for (int i = 0; i < enumNames.Length; i++)
                {
                    var enumValue = Convert.ToInt32(enumNumericValues.GetValue(i));
                    enumValues.Add(new OpenApiInteger(enumValue));
                }

                schema.Enum = enumValues;

                // Add description with enum mappings
                var enumDescriptions = new List<string>();
                for (int i = 0; i < enumNames.Length; i++)
                {
                    var enumValue = Convert.ToInt32(enumNumericValues.GetValue(i));
                    enumDescriptions.Add($"{enumValue} = {enumNames[i]}");
                }

                schema.Description += $"\n\nEnum Values:\n{string.Join("\n", enumDescriptions)}";
            }
        }
    }
}
