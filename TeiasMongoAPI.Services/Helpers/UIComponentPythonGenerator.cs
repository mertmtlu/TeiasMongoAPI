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

            // Handle table elements specifically
            if (elementType == "table")
            {
                GenerateTableProperties(sb, element);
                return;
            }

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

        private static void GenerateTableProperties(StringBuilder sb, BsonDocument element)
        {
            var elementName = element.GetValue("name", "").AsString;
            var elementLabel = element.GetValue("label", "").AsString;
            var required = element.GetValue("required", false).AsBoolean;

            // Get table configuration
            var tableConfig = element.GetValue("tableConfig", new BsonDocument()).AsBsonDocument;
            var rows = tableConfig.GetValue("rows", 3).AsInt32;
            var columns = tableConfig.GetValue("columns", 3).AsInt32;
            var cells = tableConfig.GetValue("cells", new BsonArray()).AsBsonArray;

            // Generate properties for the overall table
            sb.AppendLine($"    @property");
            sb.AppendLine($"    def {elementName}(self) -> Dict[str, Any]:");
            sb.AppendLine($"        \"\"\"");
            sb.AppendLine($"        {elementLabel} - Table data structure");
            sb.AppendLine($"        Type: table ({rows}x{columns})");
            sb.AppendLine($"        Required: {required}");
            sb.AppendLine($"        \"\"\"");
            sb.AppendLine($"        table_data = {{}}");

            // Generate individual cell properties
            foreach (var cell in cells)
            {
                var cellDoc = cell.AsBsonDocument;
                var cellId = cellDoc.GetValue("cellId", "").AsString;
                var customName = cellDoc.GetValue("customName", "").AsString;
                var cellType = cellDoc.GetValue("type", "text").AsString;

                // Use custom name if available, otherwise use cellId
                var propertyName = !string.IsNullOrEmpty(customName) ? customName : cellId;

                sb.AppendLine($"        table_data['{propertyName}'] = self._values.get('{elementName}_{cellId}', '')");
            }

            sb.AppendLine($"        return table_data");
            sb.AppendLine();

            // Generate setter for the table
            sb.AppendLine($"    @{elementName}.setter");
            sb.AppendLine($"    def {elementName}(self, value: Dict[str, Any]):");
            if (required)
            {
                sb.AppendLine($"        if not value:");
                sb.AppendLine($"            raise ValueError('{elementLabel} is required')");
            }
            sb.AppendLine($"        if not isinstance(value, dict):");
            sb.AppendLine($"            raise ValueError('Table value must be a dictionary')");

            // Set individual cell values
            foreach (var cell in cells)
            {
                var cellDoc = cell.AsBsonDocument;
                var cellId = cellDoc.GetValue("cellId", "").AsString;
                var customName = cellDoc.GetValue("customName", "").AsString;
                var propertyName = !string.IsNullOrEmpty(customName) ? customName : cellId;

                sb.AppendLine($"        if '{propertyName}' in value:");
                sb.AppendLine($"            self._values['{elementName}_{cellId}'] = value['{propertyName}']");
            }
            sb.AppendLine();

            // Generate individual cell property accessors
            foreach (var cell in cells)
            {
                var cellDoc = cell.AsBsonDocument;
                var cellId = cellDoc.GetValue("cellId", "").AsString;
                var customName = cellDoc.GetValue("customName", "").AsString;
                var cellType = cellDoc.GetValue("type", "text").AsString;

                // Use custom name if available, otherwise use cellId
                var propertyName = !string.IsNullOrEmpty(customName) ? customName : cellId;
                var cellKey = $"{elementName}_{cellId}";

                // Property getter
                sb.AppendLine($"    @property");
                sb.AppendLine($"    def {propertyName}(self) -> {GetPythonTypeForCell(cellType)}:");
                sb.AppendLine($"        \"\"\"");
                sb.AppendLine($"        Cell {cellId}");
                if (!string.IsNullOrEmpty(customName))
                {
                    sb.AppendLine($"        Custom name: {customName}");
                }
                sb.AppendLine($"        Type: {cellType}");
                sb.AppendLine($"        \"\"\"");
                sb.AppendLine($"        return self._values.get('{cellKey}', {GetDefaultValueForCell(cellType)})");
                sb.AppendLine();

                // Property setter
                sb.AppendLine($"    @{propertyName}.setter");
                sb.AppendLine($"    def {propertyName}(self, value: {GetPythonTypeForCell(cellType)}):");
                GenerateValidationForCell(sb, cellType, cellDoc);
                sb.AppendLine($"        self._values['{cellKey}'] = value");
                sb.AppendLine();
            }
        }

        private static string GetPythonTypeForCell(string cellType)
        {
            return cellType switch
            {
                "text" => "Optional[str]",
                "number" => "Optional[Union[int, float]]",
                "dropdown" => "Optional[str]",
                "date" => "Optional[str]",
                _ => "Optional[str]"
            };
        }

        private static string GetDefaultValueForCell(string cellType)
        {
            return cellType switch
            {
                "text" => "''",
                "number" => "None",
                "dropdown" => "None",
                "date" => "None",
                _ => "''"
            };
        }

        private static void GenerateValidationForCell(StringBuilder sb, string cellType, BsonDocument cellDoc)
        {
            switch (cellType)
            {
                case "number":
                    sb.AppendLine($"        if value is not None and not isinstance(value, (int, float)):");
                    sb.AppendLine($"            try:");
                    sb.AppendLine($"                value = float(value)");
                    sb.AppendLine($"            except (ValueError, TypeError):");
                    sb.AppendLine($"                raise ValueError('Value must be a number')");
                    break;
                case "dropdown":
                    if (cellDoc.Contains("options"))
                    {
                        var options = cellDoc["options"].AsBsonArray;
                        var optionsList = string.Join(", ", options.Select(o => $"'{o.AsString}'"));
                        sb.AppendLine($"        valid_options = [{optionsList}]");
                        sb.AppendLine($"        if value is not None and value not in valid_options:");
                        sb.AppendLine($"            raise ValueError(f'Invalid option: {{value}}. Valid options: {{valid_options}}')");
                    }
                    break;
            }
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

            // From JSON method - UPDATED to handle table elements
            sb.AppendLine("    def from_json(self, json_str: str):");
            sb.AppendLine("        \"\"\"Load values from JSON string\"\"\"");
            sb.AppendLine("        try:");
            sb.AppendLine("            # Try to parse as proper JSON first");
            sb.AppendLine("            data = json.loads(json_str)");
            sb.AppendLine("            self._load_from_dict(data)");
            sb.AppendLine("        except json.JSONDecodeError:");
            sb.AppendLine("            # Fallback to custom JS object parsing");
            sb.AppendLine("            data = self.parse_js_object_params(json_str)");
            sb.AppendLine("            self._load_from_dict(data)");
            sb.AppendLine();

            // Add new helper method _load_from_dict
            sb.AppendLine("    def _load_from_dict(self, data: Dict[str, Any]):");
            sb.AppendLine("        \"\"\"Load values from dictionary, handling table elements specially\"\"\"");
            sb.AppendLine("        for key, value in data.items():");
            sb.AppendLine("            # Check if this is a table element");
            sb.AppendLine("            if self._is_table_element(key) and isinstance(value, dict):");
            sb.AppendLine("                # Expand table data into individual cell keys");
            sb.AppendLine("                for cell_key, cell_value in value.items():");
            sb.AppendLine("                    # Find the actual cell ID from custom name or use the key directly");
            sb.AppendLine("                    actual_cell_id = self._find_cell_id_by_name(key, cell_key)");
            sb.AppendLine("                    if actual_cell_id:");
            sb.AppendLine("                        self._values[f'{key}_{actual_cell_id}'] = cell_value");
            sb.AppendLine("                    else:");
            sb.AppendLine("                        # If no mapping found, assume it's already a cell ID");
            sb.AppendLine("                        self._values[f'{key}_{cell_key}'] = cell_value");
            sb.AppendLine("            else:");
            sb.AppendLine("                # Regular element");
            sb.AppendLine("                self._values[key] = value");
            sb.AppendLine();

            // Add helper method to check if element is a table
            sb.AppendLine("    def _is_table_element(self, element_name: str) -> bool:");
            sb.AppendLine("        \"\"\"Check if the given element name corresponds to a table element\"\"\"");

            // Generate the table element checks based on actual elements
            if (component.Configuration != null && component.Configuration.Contains("elements"))
            {
                var elements = component.Configuration["elements"].AsBsonArray;
                var tableElements = elements.Where(e => e.AsBsonDocument.GetValue("type", "").AsString == "table").ToList();

                if (tableElements.Any())
                {
                    sb.AppendLine("        table_elements = {");
                    foreach (var tableElement in tableElements)
                    {
                        var elementDoc = tableElement.AsBsonDocument;
                        var elementName = elementDoc.GetValue("name", "").AsString;
                        sb.AppendLine($"            '{elementName}': True,");
                    }
                    sb.AppendLine("        }");
                    sb.AppendLine("        return element_name in table_elements");
                }
                else
                {
                    sb.AppendLine("        return False");
                }
            }
            else
            {
                sb.AppendLine("        return False");
            }
            sb.AppendLine();

            // Add helper method to find cell ID by custom name
            sb.AppendLine("    def _find_cell_id_by_name(self, table_name: str, cell_name: str) -> Optional[str]:");
            sb.AppendLine("        \"\"\"Find the actual cell ID by custom name or return the name if it's already a cell ID\"\"\"");

            // Generate cell mappings for each table element
            if (component.Configuration != null && component.Configuration.Contains("elements"))
            {
                var elements = component.Configuration["elements"].AsBsonArray;
                var tableElements = elements.Where(e => e.AsBsonDocument.GetValue("type", "").AsString == "table").ToList();

                if (tableElements.Any())
                {
                    sb.AppendLine("        cell_mappings = {");
                    foreach (var tableElement in tableElements)
                    {
                        var elementDoc = tableElement.AsBsonDocument;
                        var elementName = elementDoc.GetValue("name", "").AsString;
                        var tableConfig = elementDoc.GetValue("tableConfig", new BsonDocument()).AsBsonDocument;
                        var cells = tableConfig.GetValue("cells", new BsonArray()).AsBsonArray;

                        sb.AppendLine($"            '{elementName}': {{");

                        foreach (var cell in cells)
                        {
                            var cellDoc = cell.AsBsonDocument;
                            var cellId = cellDoc.GetValue("cellId", "").AsString;
                            var customName = cellDoc.GetValue("customName", "").AsString;

                            if (!string.IsNullOrEmpty(customName))
                            {
                                sb.AppendLine($"                '{customName}': '{cellId}',");
                            }
                            // Also map the cell ID to itself for direct access
                            sb.AppendLine($"                '{cellId}': '{cellId}',");
                        }

                        sb.AppendLine("            },");
                    }
                    sb.AppendLine("        }");
                    sb.AppendLine();
                    sb.AppendLine("        if table_name in cell_mappings:");
                    sb.AppendLine("            return cell_mappings[table_name].get(cell_name, cell_name)");
                    sb.AppendLine("        return cell_name");
                }
                else
                {
                    sb.AppendLine("        return cell_name");
                }
            }
            else
            {
                sb.AppendLine("        return cell_name");
            }
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
            sb.AppendLine();

            // Updated parse_js_object_params method to handle nested objects better
            sb.AppendLine("    def parse_js_object_params(self, param_string: str) -> Dict[str, Any]:");
            sb.AppendLine("        \"\"\"Parse JavaScript-style object notation with support for nested objects\"\"\"");
            sb.AppendLine("        try:");
            sb.AppendLine("            # First try to parse as JSON");
            sb.AppendLine("            return json.loads(param_string)");
            sb.AppendLine("        except json.JSONDecodeError:");
            sb.AppendLine("            pass");
            sb.AppendLine();
            sb.AppendLine("        # Fallback to custom parsing");
            sb.AppendLine("        # Remove outer quotes and braces");
            sb.AppendLine("        clean_string = param_string.strip().strip(\"'\\\"\").strip('{ }')");
            sb.AppendLine("        params = {}");
            sb.AppendLine();
            sb.AppendLine("        # Simple key-value parsing (this is a simplified version)");
            sb.AppendLine("        # For complex nested objects, JSON parsing above should work");
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
                    var elementType = elementDoc.GetValue("type", "").AsString;
                    var required = elementDoc.GetValue("required", false).AsBoolean;

                    if (required)
                    {
                        if (elementType == "table")
                        {
                            // For table elements, check if any cell has a value
                            sb.AppendLine($"        # Check table element: {elementName}");
                            sb.AppendLine($"        table_has_value = False");

                            var tableConfig = elementDoc.GetValue("tableConfig", new BsonDocument()).AsBsonDocument;
                            var cells = tableConfig.GetValue("cells", new BsonArray()).AsBsonArray;

                            foreach (var cell in cells)
                            {
                                var cellDoc = cell.AsBsonDocument;
                                var cellId = cellDoc.GetValue("cellId", "").AsString;
                                sb.AppendLine($"        if self._values.get('{elementName}_{cellId}'):");
                                sb.AppendLine($"            table_has_value = True");
                                sb.AppendLine($"            break");
                            }

                            sb.AppendLine($"        if not table_has_value:");
                            sb.AppendLine($"            errors.append('{elementLabel} is required')");
                        }
                        else
                        {
                            sb.AppendLine($"        if not self._values.get('{elementName}'):");
                            sb.AppendLine($"            errors.append('{elementLabel} is required')");
                        }
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
                "table" => "Optional[Dict[str, Any]]",
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
                "table" => "{}",
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