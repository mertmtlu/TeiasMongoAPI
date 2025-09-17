using MongoDB.Bson;
using System.Text;
using System.Text.Json;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Services.Helpers;

namespace TeiasMongoAPI.Services.Helpers
{
    public static class UIComponentCSharpGenerator
    {
        public static string GenerateUIComponentClass(UiComponent component)
        {
            var sb = new StringBuilder();

            // File header
            sb.AppendLine("// Auto-generated UIComponent.cs");
            sb.AppendLine("// This file is generated automatically from UI Component configuration");
            sb.AppendLine("// DO NOT EDIT MANUALLY - Changes will be overwritten");
            sb.AppendLine();

            // Using statements
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.IO;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using System.Text.Json;");
            sb.AppendLine("using System.Text.Json.Serialization;");
            sb.AppendLine();

            // Namespace
            sb.AppendLine("namespace TeiasProject");
            sb.AppendLine("{");

            // Class definition with XML documentation
            sb.AppendLine("    /// <summary>");
            sb.AppendLine($"    /// Auto-generated UI Component class for: {component.Name}");
            sb.AppendLine($"    /// Description: {component.Description}");
            sb.AppendLine($"    /// Type: {component.Type}");
            sb.AppendLine($"    /// Created: {component.CreatedAt:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public class UIComponent");
            sb.AppendLine("    {");

            // Private fields for metadata
            sb.AppendLine("        private readonly Dictionary<string, object> _metadata;");
            sb.AppendLine();

            // Constructor
            sb.AppendLine("        public UIComponent()");
            sb.AppendLine("        {");
            sb.AppendLine("            _metadata = new Dictionary<string, object>");
            sb.AppendLine("            {");
            sb.AppendLine($"                [\"component_name\"] = \"{EscapeString(component.Name)}\",");
            sb.AppendLine($"                [\"component_type\"] = \"{EscapeString(component.Type)}\",");
            sb.AppendLine($"                [\"component_description\"] = \"{EscapeString(component.Description)}\",");
            sb.AppendLine($"                [\"created_at\"] = \"{component.CreatedAt:yyyy-MM-dd HH:mm:ss}\"");
            sb.AppendLine("            };");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Parse configuration and generate properties
            Dictionary<string, object> config = null;
            if (component.Configuration is string configString && !string.IsNullOrWhiteSpace(configString))
            {
                try
                {
                    config = JsonSerializer.Deserialize<Dictionary<string, object>>(configString);
                }
                catch (JsonException)
                {
                    // Configuration is not valid JSON, skip property generation
                }
            }

            // Generate properties for each element
            if (config != null && config.ContainsKey("elements"))
            {
                if (config["elements"] is JsonElement elementsArray && elementsArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement element in elementsArray.EnumerateArray())
                    {
                        var elementDoc = JsonSerializer.Deserialize<Dictionary<string, object>>(element.GetRawText());
                        if (elementDoc != null)
                        {
                            GenerateElementProperty(sb, elementDoc);
                        }
                    }
                }
            }

            // Generate static Load method
            GenerateLoadMethod(sb);

            // Generate helper methods
            GenerateHelperMethods(sb);

            // Close class and namespace
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static void GenerateElementProperty(StringBuilder sb, Dictionary<string, object> element)
        {
            if (element == null) return;

            var elementId = element.TryGetValue("id", out var id) ? id?.ToString() ?? "" : "";
            var elementType = element.TryGetValue("type", out var type) ? type?.ToString() ?? "" : "";
            var elementName = element.TryGetValue("name", out var name) ? name?.ToString() ?? "" : "";
            var elementLabel = element.TryGetValue("label", out var label) ? label?.ToString() ?? "" : "";
            var placeholder = element.TryGetValue("placeholder", out var ph) ? ph?.ToString() ?? "" : "";
            var required = element.TryGetValue("required", out var req) && (req is bool b ? b : bool.TryParse(req?.ToString(), out b) && b);

            // Sanitize property name for C#
            var propertyName = SanitizePropertyName(elementName);

            // Handle special element types
            if (elementType == "table")
            {
                GenerateTableProperties(sb, element, propertyName);
                return;
            }

            if (elementType == "file_input")
            {
                GenerateFileInputProperties(sb, element, propertyName, elementName);
                return;
            }

            // Get C# type and default value
            var csharpType = GetCSharpType(elementType, element);
            var defaultValue = GetDefaultValue(elementType, element);

            // Generate property with XML documentation
            sb.AppendLine("        /// <summary>");
            sb.AppendLine($"        /// {EscapeXmlComment(elementLabel)}");
            if (!string.IsNullOrEmpty(placeholder))
            {
                sb.AppendLine($"        /// Placeholder: {EscapeXmlComment(placeholder)}");
            }
            sb.AppendLine($"        /// Type: {elementType}");
            sb.AppendLine($"        /// Required: {required}");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine($"        [JsonPropertyName(\"{elementName}\")]");
            sb.AppendLine($"        public {csharpType} {propertyName} {{ get; set; }} = {defaultValue};");
            sb.AppendLine();
        }

        private static void GenerateFileInputProperties(StringBuilder sb, Dictionary<string, object> element, string propertyName, string originalName)
        {
            if (element == null) return;

            var elementLabel = element.TryGetValue("label", out var label) ? label?.ToString() ?? "" : "";
            var required = element.TryGetValue("required", out var req) && (req is bool b ? b : bool.TryParse(req?.ToString(), out b) && b);

            // Get file input configuration
            var fileInputConfig = element.TryGetValue("fileInputConfig", out var configObj) ?
                JsonSerializer.Deserialize<Dictionary<string, object>>(configObj.ToString()) : null;

            var multiple = fileInputConfig?.TryGetValue("multiple", out var multipleObj) == true &&
                (multipleObj is bool m ? m : bool.TryParse(multipleObj?.ToString(), out m) && m);

            var maxFileSize = fileInputConfig?.TryGetValue("maxFileSize", out var sizeObj) == true ?
                (sizeObj is int s ? s : int.TryParse(sizeObj?.ToString(), out s) ? s : 10485760) : 10485760;

            // Determine the type based on multiple files
            var propertyType = multiple ? "List<Dictionary<string, object>>?" : "Dictionary<string, object>?";
            var defaultValue = multiple ? "new List<Dictionary<string, object>>()" : "null";

            // Generate property
            sb.AppendLine("        /// <summary>");
            sb.AppendLine($"        /// {EscapeXmlComment(elementLabel)}");
            sb.AppendLine($"        /// Type: file_input");
            sb.AppendLine($"        /// Multiple files: {multiple}");
            sb.AppendLine($"        /// Max file size: {maxFileSize / (1024 * 1024)}MB");
            sb.AppendLine($"        /// Required: {required}");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine($"        [JsonPropertyName(\"{originalName}\")]");
            sb.AppendLine($"        public {propertyType} {propertyName} {{ get; set; }} = {defaultValue};");
            sb.AppendLine();

            // Generate helper methods for file handling
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// Get all files for {propertyName} as a list");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        public List<Dictionary<string, object>> Get{propertyName}Files()");
            sb.AppendLine($"        {{");
            sb.AppendLine($"            if ({propertyName} == null) return new List<Dictionary<string, object>>();");
            if (multiple)
            {
                sb.AppendLine($"            return {propertyName};");
            }
            else
            {
                sb.AppendLine($"            return new List<Dictionary<string, object>> {{ {propertyName} }};");
            }
            sb.AppendLine($"        }}");
            sb.AppendLine();

            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// Get file names for {propertyName}");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        public List<string> Get{propertyName}FileNames()");
            sb.AppendLine($"        {{");
            sb.AppendLine($"            return Get{propertyName}Files()");
            sb.AppendLine($"                .Select(f => f.TryGetValue(\"fileName\", out var name) ? name?.ToString() ?? \"Unknown\" : \"Unknown\")");
            sb.AppendLine($"                .ToList();");
            sb.AppendLine($"        }}");
            sb.AppendLine();

            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// Get total size of all files for {propertyName} in bytes");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        public long Get{propertyName}TotalSize()");
            sb.AppendLine($"        {{");
            sb.AppendLine($"            return Get{propertyName}Files()");
            sb.AppendLine($"                .Sum(f => f.TryGetValue(\"fileSize\", out var size) && long.TryParse(size?.ToString(), out var s) ? s : 0);");
            sb.AppendLine($"        }}");
            sb.AppendLine();

            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// Check if {propertyName} has any files");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        public bool Has{propertyName}Files()");
            sb.AppendLine($"        {{");
            sb.AppendLine($"            return Get{propertyName}Files().Any();");
            sb.AppendLine($"        }}");
            sb.AppendLine();
        }

        private static void GenerateTableProperties(StringBuilder sb, Dictionary<string, object> element, string propertyName)
        {
            if (element == null) return;

            var elementLabel = element.TryGetValue("label", out var label) ? label?.ToString() ?? "" : "";
            var required = element.TryGetValue("required", out var req) && (req is bool b ? b : bool.TryParse(req?.ToString(), out b) && b);

            // Get table configuration
            var tableConfig = element.TryGetValue("tableConfig", out var tableConfigObj) ?
                JsonSerializer.Deserialize<Dictionary<string, object>>(tableConfigObj.ToString()) : null;

            var rows = tableConfig?.TryGetValue("rows", out var rowsObj) == true ? (rowsObj is int r ? r : int.TryParse(rowsObj?.ToString(), out r) ? r : 3) : 3;
            var columns = tableConfig?.TryGetValue("columns", out var colsObj) == true ? (colsObj is int c ? c : int.TryParse(colsObj?.ToString(), out c) ? c : 3) : 3;

            // Generate main table property
            var elementName = element.TryGetValue("name", out var n) ? n?.ToString() ?? "" : "";
            sb.AppendLine("        /// <summary>");
            sb.AppendLine($"        /// {EscapeXmlComment(elementLabel)} - Table data structure");
            sb.AppendLine($"        /// Type: table ({rows}x{columns})");
            sb.AppendLine($"        /// Required: {required}");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine($"        [JsonPropertyName(\"{elementName}\")]");
            sb.AppendLine($"        public Dictionary<string, object>? {propertyName} {{ get; set; }} = new Dictionary<string, object>();");
            sb.AppendLine();

            // Generate individual cell properties if cells are defined
            var cells = tableConfig?.TryGetValue("cells", out var cellsObj) == true ?
                JsonSerializer.Deserialize<IEnumerable<object>>(cellsObj.ToString()) : null;

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

                        var cellPropertyName = !string.IsNullOrEmpty(customName) ? SanitizePropertyName(customName) : SanitizePropertyName(cellId);
                        var cellCSharpType = GetCSharpTypeForCell(cellType);

                        sb.AppendLine("        /// <summary>");
                        sb.AppendLine($"        /// Cell {cellId}");
                        if (!string.IsNullOrEmpty(customName))
                        {
                            sb.AppendLine($"        /// Custom name: {customName}");
                        }
                        sb.AppendLine($"        /// Type: {cellType}");
                        sb.AppendLine("        /// </summary>");
                        sb.AppendLine($"        public {cellCSharpType} {cellPropertyName}");
                        sb.AppendLine($"        {{");
                        sb.AppendLine($"            get");
                        sb.AppendLine($"            {{");
                        sb.AppendLine($"                if ({propertyName}?.TryGetValue(\"{(!string.IsNullOrEmpty(customName) ? customName : cellId)}\", out var value) == true)");
                        sb.AppendLine($"                {{");
                        GenerateValueConversion(sb, cellType, "value");
                        sb.AppendLine($"                }}");
                        sb.AppendLine($"                return {GetDefaultValueForCell(cellType)};");
                        sb.AppendLine($"            }}");
                        sb.AppendLine($"            set");
                        sb.AppendLine($"            {{");
                        sb.AppendLine($"                {propertyName} ??= new Dictionary<string, object>();");
                        sb.AppendLine($"                {propertyName}[\"{(!string.IsNullOrEmpty(customName) ? customName : cellId)}\"] = value;");
                        sb.AppendLine($"            }}");
                        sb.AppendLine($"        }}");
                        sb.AppendLine();
                    }
                }
            }
        }

        private static void GenerateValueConversion(StringBuilder sb, string cellType, string valueVariable)
        {
            switch (cellType)
            {
                case "number":
                    sb.AppendLine($"                    if (double.TryParse({valueVariable}?.ToString(), out var doubleValue)) return doubleValue;");
                    sb.AppendLine($"                    if (int.TryParse({valueVariable}?.ToString(), out var intValue)) return intValue;");
                    break;
                case "text":
                case "dropdown":
                default:
                    sb.AppendLine($"                    return {valueVariable}?.ToString();");
                    break;
            }
        }

        private static void GenerateLoadMethod(StringBuilder sb)
        {
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Load UIComponent from command line arguments");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        /// <param name=\"args\">Command line arguments where first argument should be JSON string</param>");
            sb.AppendLine("        /// <returns>Loaded UIComponent instance</returns>");
            sb.AppendLine("        public static UIComponent Load(string[] args)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (args == null || args.Length == 0)");
            sb.AppendLine("            {");
            sb.AppendLine("                return new UIComponent();");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                var jsonString = args[0];");
            sb.AppendLine("                var options = new JsonSerializerOptions");
            sb.AppendLine("                {");
            sb.AppendLine("                    PropertyNameCaseInsensitive = true,");
            sb.AppendLine("                    AllowTrailingCommas = true");
            sb.AppendLine("                };");
            sb.AppendLine("                return JsonSerializer.Deserialize<UIComponent>(jsonString, options) ?? new UIComponent();");
            sb.AppendLine("            }");
            sb.AppendLine("            catch (JsonException ex)");
            sb.AppendLine("            {");
            sb.AppendLine("                Console.WriteLine($\"Failed to parse JSON arguments: {ex.Message}\");");
            sb.AppendLine("                return new UIComponent();");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        private static void GenerateHelperMethods(StringBuilder sb)
        {
            // File reading methods
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Read all text from a file in the input directory");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        /// <param name=\"fileName\">Name of the file to read</param>");
            sb.AppendLine("        /// <returns>File content as string</returns>");
            sb.AppendLine("        public static string ReadAllText(string fileName)");
            sb.AppendLine("        {");
            sb.AppendLine("            var inputDir = Path.Combine(Directory.GetCurrentDirectory(), \"input\");");
            sb.AppendLine("            var filePath = Path.Combine(inputDir, fileName);");
            sb.AppendLine("            ");
            sb.AppendLine("            if (!File.Exists(filePath))");
            sb.AppendLine("            {");
            sb.AppendLine("                throw new FileNotFoundException($\"Input file not found: {fileName}\");");
            sb.AppendLine("            }");
            sb.AppendLine("            ");
            sb.AppendLine("            return File.ReadAllText(filePath);");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Read all bytes from a file in the input directory");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        /// <param name=\"fileName\">Name of the file to read</param>");
            sb.AppendLine("        /// <returns>File content as byte array</returns>");
            sb.AppendLine("        public static byte[] ReadAllBytes(string fileName)");
            sb.AppendLine("        {");
            sb.AppendLine("            var inputDir = Path.Combine(Directory.GetCurrentDirectory(), \"input\");");
            sb.AppendLine("            var filePath = Path.Combine(inputDir, fileName);");
            sb.AppendLine("            ");
            sb.AppendLine("            if (!File.Exists(filePath))");
            sb.AppendLine("            {");
            sb.AppendLine("                throw new FileNotFoundException($\"Input file not found: {fileName}\");");
            sb.AppendLine("            }");
            sb.AppendLine("            ");
            sb.AppendLine("            return File.ReadAllBytes(filePath);");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Check if a file exists in the input directory");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        /// <param name=\"fileName\">Name of the file to check</param>");
            sb.AppendLine("        /// <returns>True if file exists, false otherwise</returns>");
            sb.AppendLine("        public static bool FileExists(string fileName)");
            sb.AppendLine("        {");
            sb.AppendLine("            var inputDir = Path.Combine(Directory.GetCurrentDirectory(), \"input\");");
            sb.AppendLine("            var filePath = Path.Combine(inputDir, fileName);");
            sb.AppendLine("            return File.Exists(filePath);");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Get all file names in the input directory");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        /// <returns>Array of file names</returns>");
            sb.AppendLine("        public static string[] GetInputFiles()");
            sb.AppendLine("        {");
            sb.AppendLine("            var inputDir = Path.Combine(Directory.GetCurrentDirectory(), \"input\");");
            sb.AppendLine("            ");
            sb.AppendLine("            if (!Directory.Exists(inputDir))");
            sb.AppendLine("            {");
            sb.AppendLine("                return Array.Empty<string>();");
            sb.AppendLine("            }");
            sb.AppendLine("            ");
            sb.AppendLine("            return Directory.GetFiles(inputDir).Select(Path.GetFileName).ToArray();");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Get the full path to a file in the input directory");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        /// <param name=\"fileName\">Name of the file</param>");
            sb.AppendLine("        /// <returns>Full path to the file</returns>");
            sb.AppendLine("        public static string GetInputFilePath(string fileName)");
            sb.AppendLine("        {");
            sb.AppendLine("            var inputDir = Path.Combine(Directory.GetCurrentDirectory(), \"input\");");
            sb.AppendLine("            return Path.Combine(inputDir, fileName);");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Get the full path to the input directory");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        /// <returns>Full path to the input directory</returns>");
            sb.AppendLine("        public static string GetInputDirectoryPath()");
            sb.AppendLine("        {");
            sb.AppendLine("            return Path.Combine(Directory.GetCurrentDirectory(), \"input\");");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Get the full path to the output directory (creates if doesn't exist)");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        /// <returns>Full path to the output directory</returns>");
            sb.AppendLine("        public static string GetOutputDirectoryPath()");
            sb.AppendLine("        {");
            sb.AppendLine("            var outputDir = Path.Combine(Directory.GetCurrentDirectory(), \"output\");");
            sb.AppendLine("            Directory.CreateDirectory(outputDir);");
            sb.AppendLine("            return outputDir;");
            sb.AppendLine("        }");
        }

        private static string SanitizePropertyName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Property";

            // Remove invalid characters and ensure it starts with a letter or underscore
            var sanitized = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (i == 0)
                {
                    if (char.IsLetter(c) || c == '_')
                        sanitized.Append(c);
                    else
                        sanitized.Append('_');
                }
                else
                {
                    if (char.IsLetterOrDigit(c) || c == '_')
                        sanitized.Append(c);
                    else
                        sanitized.Append('_');
                }
            }

            var result = sanitized.ToString();

            // Ensure it's not a C# keyword
            var keywords = new[] { "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override", "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while" };

            if (keywords.Contains(result.ToLower()))
            {
                result = "_" + result;
            }

            return result;
        }

        private static string GetCSharpType(string elementType, Dictionary<string, object> element)
        {
            var type = UIComponentTypeRegistry.GetTypeForElement(elementType);
            return GetCSharpTypeName(type);
        }

        private static string GetCSharpTypeName(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var genericArgument = type.GetGenericArguments()[0];
                // Handle nested generics if necessary in the future
                return $"List<{genericArgument.Name}>?";
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var keyType = type.GetGenericArguments()[0].Name;
                var valueType = type.GetGenericArguments()[1].Name;
                return $"Dictionary<{keyType}, {valueType}>?";
            }
            
            // Primitive/simple types
            var typeName = type.Name switch
            {
                "String" => "string?",
                "Boolean" => "bool",
                "Double" => "double?",
                "Object" => "object?",
                _ => $"{type.Name}?" // For DTOs like NamedPointDto
            };

            return typeName;
        }

        private static string GetFileInputType(Dictionary<string, object> element)
        {
            var fileInputConfig = element.TryGetValue("fileInputConfig", out var configObj) ?
                JsonSerializer.Deserialize<Dictionary<string, object>>(configObj.ToString()) : null;

            var multiple = fileInputConfig?.TryGetValue("multiple", out var multipleObj) == true &&
                (multipleObj is bool m ? m : bool.TryParse(multipleObj?.ToString(), out m) && m);

            return multiple ? "List<Dictionary<string, object>>?" : "Dictionary<string, object>?";
        }

        private static string GetCSharpTypeForCell(string cellType)
        {
            return cellType switch
            {
                "text" => "string?",
                "number" => "double?",
                "dropdown" => "string?",
                "date" => "string?",
                _ => "string?"
            };
        }

        private static string GetDefaultValue(string elementType, Dictionary<string, object> element)
        {
            var placeholder = "";
            if (element?.TryGetValue("placeholder", out var placeholderValue) == true && placeholderValue != null)
            {
                placeholder = placeholderValue.ToString() ?? "";
            }

            return elementType switch
            {
                "text_input" => !string.IsNullOrEmpty(placeholder) ? $"\"{EscapeString(placeholder)}\"" : "null",
                "textarea" => !string.IsNullOrEmpty(placeholder) ? $"\"{EscapeString(placeholder)}\"" : "null",
                "number_input" => "null",
                "dropdown" => "null",
                "checkbox" => "false",
                "radio" => "null",
                "date_input" => "null",
                "slider" => "null",
                "multi_select" => "new List<string>()",
                "file_input" => GetFileInputDefaultValue(element),
                "table" => "new Dictionary<string, object>()",
                _ => "null"
            };
        }

        private static string GetFileInputDefaultValue(Dictionary<string, object> element)
        {
            var fileInputConfig = element.TryGetValue("fileInputConfig", out var configObj) ?
                JsonSerializer.Deserialize<Dictionary<string, object>>(configObj.ToString()) : null;

            var multiple = fileInputConfig?.TryGetValue("multiple", out var multipleObj) == true &&
                (multipleObj is bool m ? m : bool.TryParse(multipleObj?.ToString(), out m) && m);

            return multiple ? "new List<Dictionary<string, object>>()" : "null";
        }

        private static string GetDefaultValueForCell(string cellType)
        {
            return cellType switch
            {
                "text" => "null",
                "number" => "null",
                "dropdown" => "null",
                "date" => "null",
                _ => "null"
            };
        }

        private static string EscapeString(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return input.Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }

        private static string EscapeXmlComment(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return input.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }
    }
}