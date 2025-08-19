using MongoDB.Bson;
using MongoDB.Bson.Serialization.Serializers;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
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
            sb.AppendLine("import os");
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

            Dictionary<string, object> config = null;
            if (component.Configuration is string configString && !string.IsNullOrWhiteSpace(configString))
            {
                try
                {
                    // This is the line that performs the conversion
                    config = JsonSerializer.Deserialize<Dictionary<string, object>>(configString);
                }
                catch (JsonException)
                {
                    // The string was not valid JSON. Log this error if needed.
                    // config will remain null.
                }
            }

            // Generate properties for each element
            if (config != null && config.ContainsKey("elements"))
            {
                // Important: The elements are likely JsonElement, so we need to handle that
                if (config["elements"] is JsonElement elementsArray && elementsArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement element in elementsArray.EnumerateArray())
                    {
                        // Deserialize each element in the array into its own dictionary
                        var elementDoc = JsonSerializer.Deserialize<Dictionary<string, object>>(element.GetRawText());
                        if (elementDoc != null)
                        {
                            GenerateElementProperty(sb, elementDoc);
                        }
                    }
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

        private static void GenerateElementProperty(StringBuilder sb, Dictionary<string, object>? element)
        {
            if (element == null) return;
            
            var elementId = element.TryGetValue("id", out var id) ? id?.ToString() ?? "" : "";
            var elementType = element.TryGetValue("type", out var type) ? type?.ToString() ?? "" : "";
            var elementName = element.TryGetValue("name", out var name) ? name?.ToString() ?? "" : "";
            var elementLabel = element.TryGetValue("label", out var label) ? label?.ToString() ?? "" : "";
            var placeholder = element.TryGetValue("placeholder", out var ph) ? ph?.ToString() ?? "" : "";
            var required = element.TryGetValue("required", out var req) && (req is bool b ? b : bool.TryParse(req?.ToString(), out b) && b);

            // Handle table elements specifically
            if (elementType == "table")
            {
                GenerateTableProperties(sb, element);
                return;
            }

            // Handle file_input elements specifically
            if (elementType == "file_input")
            {
                GenerateFileInputProperties(sb, element);
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

        private static void GenerateFileInputProperties(StringBuilder sb, Dictionary<string, object>? element)
        {
            if (element == null) return;

            var elementName = element.TryGetValue("name", out var name) ? name?.ToString() ?? "" : "";
            var elementLabel = element.TryGetValue("label", out var label) ? label?.ToString() ?? "" : "";
            var placeholder = element.TryGetValue("placeholder", out var ph) ? ph?.ToString() ?? "" : "";
            var required = element.TryGetValue("required", out var req) && (req is bool b ? b : bool.TryParse(req?.ToString(), out b) && b);

            // Get file input configuration
            var fileInputConfig = element.TryGetValue("fileInputConfig", out var configObj) ?
                JsonSerializer.Deserialize<Dictionary<string, object>>(configObj.ToString()) : null;

            var multiple = fileInputConfig?.TryGetValue("multiple", out var multipleObj) == true &&
                (multipleObj is bool m ? m : bool.TryParse(multipleObj?.ToString(), out m) && m);

            var maxFileSize = fileInputConfig?.TryGetValue("maxFileSize", out var sizeObj) == true ?
                (sizeObj is int s ? s : int.TryParse(sizeObj?.ToString(), out s) ? s : 10485760) : 10485760;

            var acceptedFileTypes = fileInputConfig?.TryGetValue("acceptedFileTypes", out var typesObj) == true ?
                JsonSerializer.Deserialize<IEnumerable<object>>(typesObj.ToString())?.Select(t => t?.ToString() ?? "").ToList() ?? new List<string>() :
                new List<string>();

            // Property getter
            sb.AppendLine($"    @property");
            sb.AppendLine($"    def {elementName}(self) -> {(multiple ? "Optional[List[Dict[str, Any]]]" : "Optional[Dict[str, Any]]")}:");
            sb.AppendLine($"        \"\"\"");
            sb.AppendLine($"        {elementLabel}");
            if (!string.IsNullOrEmpty(placeholder))
            {
                sb.AppendLine($"        Placeholder: {placeholder}");
            }
            sb.AppendLine($"        Type: file_input");
            sb.AppendLine($"        Multiple files: {multiple}");
            sb.AppendLine($"        Max file size: {maxFileSize / (1024 * 1024)}MB");
            if (acceptedFileTypes.Any())
            {
                sb.AppendLine($"        Accepted types: {string.Join(", ", acceptedFileTypes)}");
            }
            sb.AppendLine($"        Required: {required}");
            sb.AppendLine($"        \"\"\"");
            sb.AppendLine($"        return self._values.get('{elementName}', {(multiple ? "[]" : "None")})");
            sb.AppendLine();

            // Property setter
            sb.AppendLine($"    @{elementName}.setter");
            sb.AppendLine($"    def {elementName}(self, value: {(multiple ? "Optional[List[Dict[str, Any]]]" : "Optional[Dict[str, Any]]")}):");

            if (required)
            {
                if (multiple)
                {
                    sb.AppendLine($"        if not value or len(value) == 0:");
                    sb.AppendLine($"            raise ValueError('{elementLabel} is required')");
                }
                else
                {
                    sb.AppendLine($"        if value is None:");
                    sb.AppendLine($"            raise ValueError('{elementLabel} is required')");
                }
            }

            // File validation
            sb.AppendLine($"        if value is not None:");
            if (multiple)
            {
                sb.AppendLine($"            if not isinstance(value, list):");
                sb.AppendLine($"                raise ValueError('File input value must be a list when multiple=True')");
                sb.AppendLine($"            for file_data in value:");
                var acceptedTypesString = acceptedFileTypes.Any()
                    ? "[" + string.Join(", ", acceptedFileTypes.Select(t => $"'{t}'")) + "]"
                    : "[]";
                sb.AppendLine($"                self._validate_file_data(file_data, {maxFileSize}, {acceptedTypesString})");
            }
            else
            {
                sb.AppendLine($"            if isinstance(value, list):");
                sb.AppendLine($"                raise ValueError('File input value must not be a list when multiple=False')");
                var acceptedTypesString = acceptedFileTypes.Any()
                    ? "[" + string.Join(", ", acceptedFileTypes.Select(t => $"'{t}'")) + "]"
                    : "[]";
                sb.AppendLine($"            self._validate_file_data(value, {maxFileSize}, {acceptedTypesString})");
            }

            sb.AppendLine($"        self._values['{elementName}'] = value");
            sb.AppendLine();

            // Helper methods for file properties
            sb.AppendLine($"    def get_{elementName}_files(self) -> List[Dict[str, Any]]:");
            sb.AppendLine($"        \"\"\"Get all files for {elementName} as a list\"\"\"");
            sb.AppendLine($"        value = self.{elementName}");
            sb.AppendLine($"        if value is None:");
            sb.AppendLine($"            return []");
            sb.AppendLine($"        elif isinstance(value, list):");
            sb.AppendLine($"            return value");
            sb.AppendLine($"        else:");
            sb.AppendLine($"            return [value]");
            sb.AppendLine();

            sb.AppendLine($"    def get_{elementName}_file_names(self) -> List[str]:");
            sb.AppendLine($"        \"\"\"Get file names for {elementName}\"\"\"");
            sb.AppendLine($"        files = self.get_{elementName}_files()");
            sb.AppendLine($"        return [f.get('fileName', 'Unknown') for f in files]");
            sb.AppendLine();

            sb.AppendLine($"    def get_{elementName}_total_size(self) -> int:");
            sb.AppendLine($"        \"\"\"Get total size of all files for {elementName} in bytes\"\"\"");
            sb.AppendLine($"        files = self.get_{elementName}_files()");
            sb.AppendLine($"        return sum(f.get('fileSize', 0) for f in files)");
            sb.AppendLine();

            sb.AppendLine($"    def has_{elementName}_files(self) -> bool:");
            sb.AppendLine($"        \"\"\"Check if {elementName} has any files\"\"\"");
            sb.AppendLine($"        files = self.get_{elementName}_files()");
            sb.AppendLine($"        return len(files) > 0");
            sb.AppendLine();
        }

        private static void GenerateTableProperties(StringBuilder sb, Dictionary<string, object>? element)
        {
            if (element == null) return;

            var elementName = element.TryGetValue("name", out var name) ? name?.ToString() ?? "" : "";
            var elementLabel = element.TryGetValue("label", out var label) ? label?.ToString() ?? "" : "";
            var required = element.TryGetValue("required", out var req) && (req is bool b ? b : bool.TryParse(req?.ToString(), out b) && b);

            // Get table configuration

            _ = element.TryGetValue("tableConfig", out var tableConfigObj);
            var tableConfig = JsonSerializer.Deserialize<Dictionary<string, object>>(tableConfigObj.ToString());
            var rows = tableConfig?.TryGetValue("rows", out var rowsObj) == true ? (rowsObj is int r ? r : int.TryParse(rowsObj?.ToString(), out r) ? r : 3) : 3;
            var columns = tableConfig?.TryGetValue("columns", out var colsObj) == true ? (colsObj is int c ? c : int.TryParse(colsObj?.ToString(), out c) ? c : 3) : 3;
            var cells =  JsonSerializer.Deserialize<IEnumerable<object>>(tableConfig?["cells"].ToString());

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
            if (cells != null)
            {
                foreach (var cell in cells)
                {
                    var cellDoc = JsonSerializer.Deserialize<Dictionary<string, object>>(cell.ToString());
                    if (cellDoc != null)
                    {
                        var cellId = cellDoc.TryGetValue("cellId", out var cId) ? cId?.ToString() ?? "" : "";
                        var customName = cellDoc.TryGetValue("customName", out var cName) ? cName?.ToString() ?? "" : "";
                        var cellType = cellDoc.TryGetValue("type", out var cType) ? cType?.ToString() ?? "text" : "text";

                        // Use custom name if available, otherwise use cellId
                        var propertyName = !string.IsNullOrEmpty(customName) ? customName : cellId;

                        sb.AppendLine($"        table_data['{propertyName}'] = self._values.get('{elementName}_{cellId}', '')");
                    }
                }
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
            if (cells != null)
            {
                foreach (var cell in cells)
                {
                    var cellDoc = JsonSerializer.Deserialize<Dictionary<string, object>>(cell.ToString());
                    if (cellDoc != null)
                    {
                        var cellId = cellDoc.TryGetValue("cellId", out var cId) ? cId?.ToString() ?? "" : "";
                        var customName = cellDoc.TryGetValue("customName", out var cName) ? cName?.ToString() ?? "" : "";
                        var propertyName = !string.IsNullOrEmpty(customName) ? customName : cellId;

                        sb.AppendLine($"        if '{propertyName}' in value:");
                        sb.AppendLine($"            self._values['{elementName}_{cellId}'] = value['{propertyName}']");
                    }
                }
            }
            sb.AppendLine();

            // Generate individual cell property accessors
            if (cells != null)
            {
                foreach (var cell in cells)
                {
                    var cellDoc = JsonSerializer.Deserialize<Dictionary<string, object>>(cell.ToString());
                    if (cellDoc != null)
                    {
                        var cellId = cellDoc.TryGetValue("cellId", out var cId) ? cId?.ToString() ?? "" : "";
                        var customName = cellDoc.TryGetValue("customName", out var cName) ? cName?.ToString() ?? "" : "";
                        var cellType = cellDoc.TryGetValue("type", out var cType) ? cType?.ToString() ?? "text" : "text";

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

        private static void GenerateValidationForCell(StringBuilder sb, string cellType, Dictionary<string, object>? cellDoc)
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
                    if (cellDoc?.TryGetValue("options", out var optionsObj) == true)
                    {
                        var options = JsonSerializer.Deserialize<IEnumerable<object>>(optionsObj.ToString());
                        if (options != null)
                        {
                            var optionsList = string.Join(", ", options.Select(o => $"'{o?.ToString() ?? ""}'"));
                            sb.AppendLine($"        valid_options = [{optionsList}]");
                            sb.AppendLine($"        if value is not None and value not in valid_options:");
                            sb.AppendLine($"            raise ValueError(f'Invalid option: {{value}}. Valid options: {{valid_options}}')");
                        }
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
            sb.AppendLine("            if isinstance(value, str):");
            sb.AppendLine("                _temp = self.parse_js_object_params(value)");
            sb.AppendLine("                if isinstance(_temp, dict) and _temp:");
            sb.AppendLine("                    value = _temp");
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

            sb.AppendLine("    def _validate_file_data(self, file_data: Dict[str, Any], max_size: int, accepted_types: List[str]):");
            sb.AppendLine("        \"\"\"Validate individual file data structure - supports both old and new formats\"\"\"");
            sb.AppendLine("        if not isinstance(file_data, dict):");
            sb.AppendLine("            raise ValueError('File data must be a dictionary')");
            sb.AppendLine();
            sb.AppendLine("        # Detect format: new format has 'filename' and 'content', old format has 'attachmentId' and 'fileName'");
            sb.AppendLine("        is_new_format = 'filename' in file_data and 'content' in file_data");
            sb.AppendLine("        is_old_format = 'attachmentId' in file_data and 'fileName' in file_data");
            sb.AppendLine();
            sb.AppendLine("        if not is_new_format and not is_old_format:");
            sb.AppendLine("            raise ValueError('File data must contain either (filename + content) or (attachmentId + fileName)')");
            sb.AppendLine();
            sb.AppendLine("        # Get filename and file size based on format");
            sb.AppendLine("        if is_new_format:");
            sb.AppendLine("            file_name = file_data.get('filename', '')");
            sb.AppendLine("            file_size = file_data.get('fileSize', 0)");
            sb.AppendLine("            content_type = file_data.get('contentType', '')");
            sb.AppendLine("            has_content = bool(file_data.get('content', ''))");
            sb.AppendLine("        else:");
            sb.AppendLine("            file_name = file_data.get('fileName', '')");
            sb.AppendLine("            file_size = file_data.get('fileSize', 0)");
            sb.AppendLine("            content_type = file_data.get('contentType', '')");
            sb.AppendLine("            has_content = file_data.get('uploaded', False)");
            sb.AppendLine();
            sb.AppendLine("        # Validate file size");
            sb.AppendLine("        if file_size > max_size:");
            sb.AppendLine("            max_mb = max_size / (1024 * 1024)");
            sb.AppendLine("            actual_mb = file_size / (1024 * 1024)");
            sb.AppendLine("            raise ValueError(f'File {file_name} is {actual_mb:.1f}MB, maximum allowed is {max_mb:.1f}MB')");
            sb.AppendLine();
            sb.AppendLine("        # Validate file type");
            sb.AppendLine("        if accepted_types and '*/*' not in accepted_types:");
            sb.AppendLine("            file_extension = '.' + file_name.split('.')[-1].lower() if '.' in file_name else ''");
            sb.AppendLine("            ");
            sb.AppendLine("            type_valid = False");
            sb.AppendLine("            for accepted_type in accepted_types:");
            sb.AppendLine("                if accepted_type.startswith('.'):");
            sb.AppendLine("                    # File extension check");
            sb.AppendLine("                    if file_extension == accepted_type.lower():");
            sb.AppendLine("                        type_valid = True");
            sb.AppendLine("                        break");
            sb.AppendLine("                elif accepted_type.endswith('/*'):");
            sb.AppendLine("                    # MIME type wildcard check");
            sb.AppendLine("                    base_type = accepted_type.split('/')[0]");
            sb.AppendLine("                    if content_type.startswith(base_type + '/'):");
            sb.AppendLine("                        type_valid = True");
            sb.AppendLine("                        break");
            sb.AppendLine("                elif accepted_type == content_type:");
            sb.AppendLine("                    # Exact MIME type match");
            sb.AppendLine("                    type_valid = True");
            sb.AppendLine("                    break");
            sb.AppendLine("            ");
            sb.AppendLine("            if not type_valid:");
            sb.AppendLine("                raise ValueError(f'File type {content_type} not allowed for {file_name}. Accepted types: {accepted_types}')");
            sb.AppendLine();
            sb.AppendLine("        # Check if file has content (uploaded successfully or has content data)");
            sb.AppendLine("        if not has_content:");
            sb.AppendLine("            if is_new_format:");
            sb.AppendLine("                raise ValueError(f'File {file_name} has no content data')");
            sb.AppendLine("            else:");
            sb.AppendLine("                raise ValueError(f'File {file_name} was not uploaded successfully')");
            sb.AppendLine();

            // Add helper method to check if element is a table
            sb.AppendLine("    def _is_table_element(self, element_name: str) -> bool:");
            sb.AppendLine("        \"\"\"Check if the given element name corresponds to a table element\"\"\"");

            // Generate the table element checks based on actual elements
            var tableConfig = JsonSerializer.Deserialize<Dictionary<string, object>>(component.Configuration.ToString());
            if (tableConfig.ContainsKey("elements"))
            {
                var elements = JsonSerializer.Deserialize<IEnumerable<object>>(tableConfig["elements"].ToString());
                var tableElements = elements?.Where(e => {
                    var elementDict = JsonSerializer.Deserialize<Dictionary<string, object>>(e.ToString());
                    return elementDict?.TryGetValue("type", out var type) == true && type?.ToString() == "table";
                }).ToList() ?? new List<object>();

                if (tableElements.Any())
                {
                    sb.AppendLine("        table_elements = {");
                    foreach (var tableElement in tableElements)
                    {
                        var elementDoc = JsonSerializer.Deserialize<Dictionary<string, object>>(tableElement.ToString());
                        if (elementDoc != null)
                        {
                            var elementName = elementDoc.TryGetValue("name", out var nameObj) ? nameObj?.ToString() ?? "" : "";
                            sb.AppendLine($"            '{elementName}': True,");
                        }
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
            var configDict = JsonSerializer.Deserialize<Dictionary<string, object>>(component.Configuration.ToString());
            if (configDict.ContainsKey("elements"))
            {
                var elements = JsonSerializer.Deserialize<IEnumerable<object>>(configDict["elements"].ToString());
                var tableElements = elements?.Where(e => {
                    var elementDict = JsonSerializer.Deserialize<Dictionary<string, object>>(e.ToString());
                    return elementDict?.TryGetValue("type", out var type) == true && type?.ToString() == "table";
                }).ToList() ?? new List<object>();

                if (tableElements.Any())
                {
                    sb.AppendLine("        cell_mappings = {");
                    foreach (var tableElement in tableElements)
                    {
                        var elementDoc = JsonSerializer.Deserialize<Dictionary<string, object>>(tableElement.ToString());
                        if (elementDoc != null)
                        {
                            var elementName = elementDoc.TryGetValue("name", out var nameObj) ? nameObj?.ToString() ?? "" : "";
                            var elementTableConfig = elementDoc.TryGetValue("tableConfig", out var tableConfigObj) ? JsonSerializer.Deserialize<Dictionary<string, object>>(tableConfigObj.ToString()) : null;
                            var cells = elementTableConfig?.TryGetValue("cells", out var cellsObj) == true ? JsonSerializer.Deserialize<IEnumerable<object>>(cellsObj.ToString()) : null;

                            sb.AppendLine($"            '{elementName}': {{");

                            if (cells != null)
                            {
                                foreach (var cell in cells)
                                {
                                    var cellDoc = JsonSerializer.Deserialize<Dictionary<string, object>>(cell.ToString());
                                    if (cellDoc != null)
                                    {
                                        var cellId = cellDoc.TryGetValue("cellId", out var cellIdObj) ? cellIdObj?.ToString() ?? "" : "";
                                        var customName = cellDoc.TryGetValue("customName", out var customNameObj) ? customNameObj?.ToString() ?? "" : "";

                                        if (!string.IsNullOrEmpty(customName))
                                        {
                                            sb.AppendLine($"                '{customName}': '{cellId}',");
                                        }
                                        // Also map the cell ID to itself for direct access
                                        sb.AppendLine($"                '{cellId}': '{cellId}',");
                                    }
                                }
                            }

                            sb.AppendLine("            },");
                        }
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
            sb.AppendLine("        clean_string = param_string.strip().strip(\"'\\\"\").removeprefix('{').removesuffix('}')");
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

            // Input file access methods
            sb.AppendLine("    def get_input_files(self) -> List[str]:");
            sb.AppendLine("        \"\"\"Get list of all input file names available in the input directory\"\"\"");
            sb.AppendLine("        input_dir = os.path.join(os.getcwd(), 'input')");
            sb.AppendLine("        if not os.path.exists(input_dir):");
            sb.AppendLine("            return []");
            sb.AppendLine("        return [f for f in os.listdir(input_dir) if os.path.isfile(os.path.join(input_dir, f))]");
            sb.AppendLine();

            sb.AppendLine("    def get_input_file(self, filename: str) -> str:");
            sb.AppendLine("        \"\"\"Get full absolute path to input file by name\"\"\"");
            sb.AppendLine("        input_dir = os.path.join(os.getcwd(), 'input')");
            sb.AppendLine("        file_path = os.path.join(input_dir, filename)");
            sb.AppendLine("        if not os.path.exists(file_path):");
            sb.AppendLine("            raise FileNotFoundError(f'Input file not found: {filename}')");
            sb.AppendLine("        return file_path");
            sb.AppendLine();

            sb.AppendLine("    def get_input_file_path(self, filename: str) -> str:");
            sb.AppendLine("        \"\"\"Get relative path to input file from project directory\"\"\"");
            sb.AppendLine("        input_dir = os.path.join(os.getcwd(), 'input')");
            sb.AppendLine("        file_path = os.path.join(input_dir, filename)");
            sb.AppendLine("        if not os.path.exists(file_path):");
            sb.AppendLine("            raise FileNotFoundError(f'Input file not found: {filename}')");
            sb.AppendLine("        return os.path.join('input', filename)");
            sb.AppendLine();

            sb.AppendLine("    def has_input_file(self, filename: str) -> bool:");
            sb.AppendLine("        \"\"\"Check if specific input file exists\"\"\"");
            sb.AppendLine("        input_dir = os.path.join(os.getcwd(), 'input')");
            sb.AppendLine("        file_path = os.path.join(input_dir, filename)");
            sb.AppendLine("        return os.path.exists(file_path) and os.path.isfile(file_path)");
            sb.AppendLine();

            sb.AppendLine("    def read_input_file(self, filename: str, mode: str = 'r', encoding: str = 'utf-8') -> Union[str, bytes]:");
            sb.AppendLine("        \"\"\"Read input file contents. Use mode='rb' for binary files\"\"\"");
            sb.AppendLine("        file_path = self.get_input_file(filename)");
            sb.AppendLine("        if 'b' in mode:");
            sb.AppendLine("            with open(file_path, mode) as f:");
            sb.AppendLine("                return f.read()");
            sb.AppendLine("        else:");
            sb.AppendLine("            with open(file_path, mode, encoding=encoding) as f:");
            sb.AppendLine("                return f.read()");
            sb.AppendLine();

            // Validate method
            sb.AppendLine("    def validate(self) -> List[str]:");
            sb.AppendLine("        \"\"\"Validate all required fields and return list of errors\"\"\"");
            sb.AppendLine("        errors = []");

            if (component.Configuration is Dictionary<string, object> validationConfig && validationConfig.ContainsKey("elements"))
            {
                var elements = JsonSerializer.Deserialize<IEnumerable<object>>(validationConfig["elements"].ToString());
                if (elements != null)
                {
                    foreach (var element in elements)
                    {
                        var elementDoc = JsonSerializer.Deserialize<Dictionary<string, object>>(element.ToString());
                        if (elementDoc != null)
                        {
                            var elementName = elementDoc.TryGetValue("name", out var name) ? name?.ToString() ?? "" : "";
                            var elementLabel = elementDoc.TryGetValue("label", out var label) ? label?.ToString() ?? "" : "";
                            var elementType = elementDoc.TryGetValue("type", out var type) ? type?.ToString() ?? "" : "";
                            var required = elementDoc.TryGetValue("required", out var req) && (req is bool b ? b : bool.TryParse(req?.ToString(), out b) && b);

                    if (required)
                    {
                        if (elementType == "table")
                        {
                            // For table elements, check if any cell has a value
                            sb.AppendLine($"        # Check table element: {elementName}");
                            sb.AppendLine($"        table_has_value = False");

                            var validationTableConfig = elementDoc.TryGetValue("tableConfig", out var tableConfigObj) ? JsonSerializer.Deserialize<Dictionary<string, object>>(tableConfigObj.ToString()) : null;
                            var cells = validationTableConfig?.TryGetValue("cells", out var cellsObj) == true ? JsonSerializer.Deserialize<IEnumerable<object>>(cellsObj.ToString()): null;

                            if (cells != null)
                            {
                                foreach (var cell in cells)
                                {
                                    var cellDoc = JsonSerializer.Deserialize<Dictionary<string, object>>(cell.ToString());
                                    var cellId = cellDoc?.TryGetValue("cellId", out var cellIdObj) == true ? cellIdObj?.ToString() ?? "" : "";
                                    if (!string.IsNullOrEmpty(cellId))
                                    {
                                        sb.AppendLine($"        if self._values.get('{elementName}_{cellId}'):");
                                        sb.AppendLine($"            table_has_value = True");
                                        sb.AppendLine($"            break");
                                    }
                                }
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
                "file_input" => "Optional[Union[Dict[str, Any], List[Dict[str, Any]]]]", // Updated
                "date_input" => "Optional[str]",
                "slider" => "Optional[Union[int, float]]",
                "multi_select" => "Optional[List[str]]",
                "table" => "Optional[Dict[str, Any]]",
                _ => "Optional[Any]"
            };
        }

        private static string GetDefaultValue(string elementType, Dictionary<string, object>? element)
        {
            var placeholder = "";
            if (element?.TryGetValue("placeholder", out var placeholderValue) == true && placeholderValue != null)
            {
                placeholder = placeholderValue.ToString() ?? "";
            }

            // Check for file input multiple configuration
            if (elementType == "file_input")
            {
                var fileInputConfig = element?.TryGetValue("fileInputConfig", out var configObj) == true ?
                    JsonSerializer.Deserialize<Dictionary<string, object>>(configObj.ToString()) : null;

                var multiple = fileInputConfig?.TryGetValue("multiple", out var multipleObj) == true &&
                    (multipleObj is bool m ? m : bool.TryParse(multipleObj?.ToString(), out m) && m);

                return multiple ? "[]" : "None";
            }

            return elementType switch
            {
                "text_input" => !string.IsNullOrEmpty(placeholder) ? $"'{placeholder}'" : "None",
                "textarea" => !string.IsNullOrEmpty(placeholder) ? $"'{placeholder}'" : "None",
                "number_input" => "None",
                "dropdown" => "None",
                "checkbox" => "False",
                "radio" => "None",
                "date_input" => "None",
                "slider" => "None",
                "multi_select" => "[]",
                "table" => "{}",
                _ => "None"
            };
        }

        private static void GenerateValidation(StringBuilder sb, string elementType, Dictionary<string, object>? element)
        {
            switch (elementType)
            {
                case "dropdown":
                    if (element?.TryGetValue("options", out var optionsObj) == true)
                    {
                        var options = JsonSerializer.Deserialize<IEnumerable<object>>(optionsObj.ToString());
                        if (options != null)
                        {
                            var optionsList = string.Join(", ", options.Select(o => $"'{o?.ToString() ?? ""}'"));
                            sb.AppendLine($"        valid_options = [{optionsList}]");
                            sb.AppendLine($"        if value is not None and value not in valid_options:");
                            sb.AppendLine($"            raise ValueError(f'Invalid option: {{value}}. Valid options: {{valid_options}}')");
                        }
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
                case "file_input":
                    // File input validation is handled in GenerateFileInputProperties
                    break;
            }
        }
    }
}