using MongoDB.Bson;
using System.Text;
using TeiasMongoAPI.Core.Models.Collaboration;

namespace TeiasMongoAPI.Services.Helpers
{
    public static class UIComponentPythonGenerator
    {
        public static string GenerateUIComponentPython(UiComponent component)
        {
            var sb = new StringBuilder();

            // File header
            sb.AppendLine("# Auto-generated UIComponent.py");
            sb.AppendLine("# This file is generated automatically from UI Component configuration");
            sb.AppendLine("# DO NOT EDIT MANUALLY - Changes will be overwritten");
            sb.AppendLine();
            sb.AppendLine("from typing import Optional, List, Dict, Any, Union");
            sb.AppendLine("import json");
            sb.AppendLine("import re");
            sb.AppendLine("import sys");
            sb.AppendLine();

            // Component class
            sb.AppendLine($"class UIComponent:");
            sb.AppendLine($"    \"\"\"");
            sb.AppendLine($"    Auto-generated UI Component class for: {component.Name}");
            sb.AppendLine($"    Description: {component.Description}");
            sb.AppendLine($"    Type: {component.Type}");
            sb.AppendLine($"    Created: {component.CreatedAt:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"    \"\"\"");
            sb.AppendLine();

            // Initialize method
            sb.AppendLine("    def __init__(self):");
            sb.AppendLine("        self._values = {}");
            sb.AppendLine("        self._metadata = {");
            sb.AppendLine($"            'component_name': '{component.Name}',");
            sb.AppendLine($"            'component_type': '{component.Type}',");
            sb.AppendLine($"            'component_description': '{component.Description}',");
            sb.AppendLine($"            'created_at': '{component.CreatedAt:yyyy-MM-dd HH:mm:ss}'");
            sb.AppendLine("        }");
            sb.AppendLine("        self.from_json(sys.argv[1])");
            sb.AppendLine();

            // Generate properties for each element
            if (component.Configuration != null && component.Configuration.Contains("elements"))
            {
                var elements = component.Configuration["elements"].AsBsonArray;
                foreach (var element in elements)
                {
                    var elementDoc = element.AsBsonDocument;
                    GenerateElementProperty(sb, elementDoc);
                }
            }

            // Helper methods
            GenerateHelperMethods(sb, component);

            // Global ui instance
            sb.AppendLine();
            sb.AppendLine("# Global UI component instance");
            sb.AppendLine("ui = UIComponent()");

            return sb.ToString();
        }

        private static void GenerateElementProperty(StringBuilder sb, BsonDocument element)
        {
            var elementId = element.GetValue("id", "").AsString;
            var elementType = element.GetValue("type", "").AsString;
            var elementName = element.GetValue("name", "").AsString;
            var elementLabel = element.GetValue("label", "").AsString;
            var placeholder = element.GetValue("placeholder", "").AsString;
            var required = element.GetValue("required", false).AsBoolean;

            // Property getter
            sb.AppendLine($"    @property");
            sb.AppendLine($"    def {elementName}(self) -> {GetPythonType(elementType)}:");
            sb.AppendLine($"        \"\"\"");
            sb.AppendLine($"        {elementLabel}");
            if (!string.IsNullOrEmpty(placeholder))
            {
                sb.AppendLine($"        Placeholder: {placeholder}");
            }
            sb.AppendLine($"        Type: {elementType}");
            sb.AppendLine($"        Required: {required}");
            sb.AppendLine($"        \"\"\"");
            sb.AppendLine($"        return self._values.get('{elementName}', {GetDefaultValue(elementType, element)})");
            sb.AppendLine();

            // Property setter
            sb.AppendLine($"    @{elementName}.setter");
            sb.AppendLine($"    def {elementName}(self, value: {GetPythonType(elementType)}):");
            if (required)
            {
                sb.AppendLine($"        if value is None or value == '':");
                sb.AppendLine($"            raise ValueError('{elementLabel} is required')");
            }
            GenerateValidation(sb, elementType, element);
            sb.AppendLine($"        self._values['{elementName}'] = value");
            sb.AppendLine();
        }

        private static void GenerateHelperMethods(StringBuilder sb, UiComponent component)
        {
            // Get all values method
            sb.AppendLine("    def get_all_values(self) -> Dict[str, Any]:");
            sb.AppendLine("        \"\"\"Get all current UI component values\"\"\"");
            sb.AppendLine("        return self._values.copy()");
            sb.AppendLine();

            // Get value method
            sb.AppendLine("    def get_value(self, name: str, default: Any = None) -> Any:");
            sb.AppendLine("        \"\"\"Get value by element name\"\"\"");
            sb.AppendLine("        return self._values.get(name, default)");
            sb.AppendLine();

            // Set value method
            sb.AppendLine("    def set_value(self, name: str, value: Any):");
            sb.AppendLine("        \"\"\"Set value by element name\"\"\"");
            sb.AppendLine("        self._values[name] = value");
            sb.AppendLine();

            // To JSON method
            sb.AppendLine("    def to_json(self) -> str:");
            sb.AppendLine("        \"\"\"Convert all values to JSON string\"\"\"");
            sb.AppendLine("        return json.dumps(self._values, default=str)");
            sb.AppendLine();

            // From JSON method
            sb.AppendLine("    def from_json(self, json_str: str):");
            sb.AppendLine("        \"\"\"Load values from JSON string\"\"\"");
            sb.AppendLine("        data = self.parse_js_object_params(json_str)");
            sb.AppendLine("        self._values.update(data)");
            sb.AppendLine();

            sb.AppendLine("    def parse_value(self, value_str):");
            sb.AppendLine("        \"\"\"Convert string value to appropriate Python type\"\"\"");
            sb.AppendLine("        value_str = value_str.strip()");
            sb.AppendLine();
            sb.AppendLine("        if value_str.lower() == 'true':");
            sb.AppendLine("            return True");
            sb.AppendLine("        elif value_str.lower() == 'false':");
            sb.AppendLine("            return False");
            sb.AppendLine("        elif value_str.lower() == 'null':");
            sb.AppendLine("            return None");
            sb.AppendLine("        elif value_str.isdigit():");
            sb.AppendLine("            return int(value_str)");
            sb.AppendLine("        elif re.match(r'^-?\\d+\\.\\d+$', value_str):");
            sb.AppendLine("            return float(value_str)");
            sb.AppendLine("        else:");
            sb.AppendLine("            return value_str");

            
            // JSON parse method
            sb.AppendLine("    def parse_js_object_params(self, param_string: str):");
            sb.AppendLine("        \"\"\"Parse JavaScript-style object notation\"\"\"");
            sb.AppendLine();
            sb.AppendLine("        # Remove outer quotes and braces");
            sb.AppendLine("        clean_string = param_string.strip().strip(\"'\\\"\").strip('{ }')");
            sb.AppendLine("        params = {}");
            sb.AppendLine();
            sb.AppendLine("        # Split by comma, but handle nested structures");
            sb.AppendLine("        current_key = \"\"");
            sb.AppendLine("        current_value = \"\"");
            sb.AppendLine("        in_key = True");
            sb.AppendLine("        bracket_count = 0");
            sb.AppendLine("        i = 0");
            sb.AppendLine();
            sb.AppendLine("        while i < len(clean_string):");
            sb.AppendLine("            char = clean_string[i]");
            sb.AppendLine("            if char == ':' and bracket_count == 0 and in_key:");
            sb.AppendLine("                in_key = False");
            sb.AppendLine("                i += 1");
            sb.AppendLine("                continue");
            sb.AppendLine("            elif char == ',' and bracket_count == 0 and not in_key:");
            sb.AppendLine("                # Process the key-value pair");
            sb.AppendLine("                if current_key and current_value:");
            sb.AppendLine("                    params[current_key.strip()] = self.parse_value(current_value.strip())");
            sb.AppendLine("                current_key = \"\"");
            sb.AppendLine("                current_value = \"\"");
            sb.AppendLine("                in_key = True");
            sb.AppendLine("                i += 1");
            sb.AppendLine("                continue");
            sb.AppendLine("            elif char in '([{':");
            sb.AppendLine("                bracket_count += 1");
            sb.AppendLine("            elif char in ')]}':");
            sb.AppendLine("                bracket_count -= 1");
            sb.AppendLine("            if in_key:");
            sb.AppendLine("                current_key += char");
            sb.AppendLine("            else:");
            sb.AppendLine("                current_value += char");
            sb.AppendLine("            i += 1");
            sb.AppendLine();
            sb.AppendLine("        # Process the last pair");
            sb.AppendLine("        if current_key and current_value:");
            sb.AppendLine("            params[current_key.strip()] = self.parse_value(current_value.strip())");
            sb.AppendLine();
            sb.AppendLine("        return params");
            sb.AppendLine();

            // Validate method
            sb.AppendLine("    def validate(self) -> List[str]:");
            sb.AppendLine("        \"\"\"Validate all required fields and return list of errors\"\"\"");
            sb.AppendLine("        errors = []");

            if (component.Configuration != null && component.Configuration.Contains("elements"))
            {
                var elements = component.Configuration["elements"].AsBsonArray;
                foreach (var element in elements)
                {
                    var elementDoc = element.AsBsonDocument;
                    var elementName = elementDoc.GetValue("name", "").AsString;
                    var elementLabel = elementDoc.GetValue("label", "").AsString;
                    var required = elementDoc.GetValue("required", false).AsBoolean;

                    if (required)
                    {
                        sb.AppendLine($"        if not self._values.get('{elementName}'):");
                        sb.AppendLine($"            errors.append('{elementLabel} is required')");
                    }
                }
            }

            sb.AppendLine("        return errors");
            sb.AppendLine();

            // Reset method
            sb.AppendLine("    def reset(self):");
            sb.AppendLine("        \"\"\"Reset all values to defaults\"\"\"");
            sb.AppendLine("        self._values.clear()");
            sb.AppendLine();

            // String representation
            sb.AppendLine("    def __str__(self):");
            sb.AppendLine("        return f\"UIComponent({self._metadata['component_name']}): {len(self._values)} values\"");
            sb.AppendLine();

            // Representation
            sb.AppendLine("    def __repr__(self):");
            sb.AppendLine("        return f\"UIComponent(name='{component.Name}', type='{component.Type}', values={self._values})\"");
        }

        private static string GetPythonType(string elementType)
        {
            return elementType switch
            {
                "text_input" => "Optional[str]",
                "textarea" => "Optional[str]",
                "number_input" => "Optional[Union[int, float]]",
                "dropdown" => "Optional[str]",
                "checkbox" => "Optional[bool]",
                "radio" => "Optional[str]",
                "file_input" => "Optional[str]",
                "date_input" => "Optional[str]",
                "slider" => "Optional[Union[int, float]]",
                "multi_select" => "Optional[List[str]]",
                _ => "Optional[Any]"
            };
        }

        private static string GetDefaultValue(string elementType, BsonDocument element)
        {
            var placeholder = element.GetValue("placeholder", "").AsString;

            return elementType switch
            {
                "text_input" => !string.IsNullOrEmpty(placeholder) ? $"'{placeholder}'" : "None",
                "textarea" => !string.IsNullOrEmpty(placeholder) ? $"'{placeholder}'" : "None",
                "number_input" => "None",
                "dropdown" => "None",
                "checkbox" => "False",
                "radio" => "None",
                "file_input" => "None",
                "date_input" => "None",
                "slider" => "None",
                "multi_select" => "[]",
                _ => "None"
            };
        }

        private static void GenerateValidation(StringBuilder sb, string elementType, BsonDocument element)
        {
            switch (elementType)
            {
                case "dropdown":
                    if (element.Contains("options"))
                    {
                        var options = element["options"].AsBsonArray;
                        var optionsList = string.Join(", ", options.Select(o => $"'{o.AsString}'"));
                        sb.AppendLine($"        valid_options = [{optionsList}]");
                        sb.AppendLine($"        if value is not None and value not in valid_options:");
                        sb.AppendLine($"            raise ValueError(f'Invalid option: {{value}}. Valid options: {{valid_options}}')");
                    }
                    break;
                case "number_input":
                    sb.AppendLine($"        if value is not None and not isinstance(value, (int, float)):");
                    sb.AppendLine($"            raise ValueError('Value must be a number')");
                    break;
                case "checkbox":
                    sb.AppendLine($"        if value is not None and not isinstance(value, bool):");
                    sb.AppendLine($"            raise ValueError('Value must be a boolean')");
                    break;
                case "multi_select":
                    sb.AppendLine($"        if value is not None and not isinstance(value, list):");
                    sb.AppendLine($"            raise ValueError('Value must be a list')");
                    break;
            }
        }
    }
}