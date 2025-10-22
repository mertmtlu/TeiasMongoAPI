using System.Text;
using Microsoft.Extensions.Logging;
using TeiasMongoAPI.Services.Interfaces;

namespace TeiasMongoAPI.Services.Services.Implementations.AI
{
    /// <summary>
    /// Generates virtual UIComponent.py module content for AI context
    /// This helps the AI understand the dynamically-generated UI component structure
    /// </summary>
    public class UIComponentContextGenerator
    {
        private readonly IUiComponentService _uiComponentService;
        private readonly ILogger<UIComponentContextGenerator> _logger;

        public UIComponentContextGenerator(
            IUiComponentService uiComponentService,
            ILogger<UIComponentContextGenerator> logger)
        {
            _uiComponentService = uiComponentService;
            _logger = logger;
        }

        /// <summary>
        /// Generate a virtual UIComponent.py module representing the UI components
        /// registered for this program/version
        /// </summary>
        public async Task<string?> GenerateVirtualUIComponentModuleAsync(
            string programId,
            string versionId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Get UI component mappings for this program/version
                var componentMappings = await _uiComponentService.GetProgramVersionComponentMappingsAsync(
                    programId,
                    versionId,
                    cancellationToken);

                if (componentMappings == null || !componentMappings.Any())
                {
                    // No UI components registered
                    return null;
                }

                var sb = new StringBuilder();

                sb.AppendLine("\"\"\"");
                sb.AppendLine("VIRTUAL MODULE - Dynamically Generated at Runtime");
                sb.AppendLine("=====================================");
                sb.AppendLine("This module is automatically generated when the program executes.");
                sb.AppendLine("It provides access to UI components registered for this program.");
                sb.AppendLine();
                sb.AppendLine("Note: This file does NOT exist in your project source code.");
                sb.AppendLine("It is created dynamically by the execution engine.");
                sb.AppendLine("\"\"\"");
                sb.AppendLine();
                sb.AppendLine("# UI Component Registry");
                sb.AppendLine("# This object is populated with all registered UI components");
                sb.AppendLine();
                sb.AppendLine("class UIComponentRegistry:");
                sb.AppendLine("    \"\"\"Container for all UI component instances\"\"\"");
                sb.AppendLine("    ");
                sb.AppendLine("    def __init__(self):");
                sb.AppendLine("        # Initialize registered UI components");

                // Group components by type
                var componentsByType = componentMappings.GroupBy(c => c.ComponentName);

                foreach (var group in componentsByType)
                {
                    var componentName = group.Key;
                    var instances = group.ToList();

                    sb.AppendLine($"        # {componentName} instances");

                    foreach (var instance in instances)
                    {
                        var mappingName = instance.MappingName;
                        var config = instance.MappingConfiguration;

                        sb.AppendLine($"        self.{mappingName} = {{");

                        // Add configuration keys as example structure
                        if (config != null && config.Any())
                        {
                            foreach (var kvp in config)
                            {
                                var value = SerializeValue(kvp.Value);
                                sb.AppendLine($"            '{kvp.Key}': {value},");
                            }
                        }
                        else
                        {
                            sb.AppendLine($"            # Component configuration will be available at runtime");
                        }

                        sb.AppendLine($"        }}");
                        sb.AppendLine();
                    }
                }

                sb.AppendLine();
                sb.AppendLine("# Create the global 'ui' instance");
                sb.AppendLine("ui = UIComponentRegistry()");
                sb.AppendLine();
                sb.AppendLine("# Example usage:");
                sb.AppendLine("# from UIComponent import ui");

                if (componentMappings.Any())
                {
                    var firstMapping = componentMappings.First();
                    sb.AppendLine($"# value = ui.{firstMapping.MappingName}");

                    if (firstMapping.MappingConfiguration?.Any() == true)
                    {
                        var firstKey = firstMapping.MappingConfiguration.First().Key;
                        sb.AppendLine($"# specific_value = ui.{firstMapping.MappingName}['{firstKey}']");
                    }
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate virtual UIComponent module for program {ProgramId}, version {VersionId}",
                    programId, versionId);
                return null;
            }
        }

        private string SerializeValue(object? value)
        {
            if (value == null)
                return "None";

            if (value is string strValue)
                return $"'{strValue}'";

            if (value is bool boolValue)
                return boolValue ? "True" : "False";

            if (value is int || value is long || value is double || value is decimal)
                return value.ToString() ?? "0";

            if (value is System.Collections.IEnumerable enumerable && value is not string)
            {
                var items = new List<string>();
                foreach (var item in enumerable)
                {
                    items.Add(SerializeValue(item));
                }
                return $"[{string.Join(", ", items)}]";
            }

            // For complex objects, return a placeholder
            return $"{{{repr(value)}}}";
        }

        private string repr(object value)
        {
            return $"'# {value.GetType().Name} object'";
        }
    }
}
