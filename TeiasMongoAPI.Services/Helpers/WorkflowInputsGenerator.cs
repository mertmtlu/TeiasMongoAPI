using MongoDB.Bson;
using System.Text;
using System.Text.Json;
using TeiasMongoAPI.Core.Models.Collaboration;

namespace TeiasMongoAPI.Services.Helpers
{
    public static class WorkflowInputsGenerator
    {
        public static string GenerateWorkflowInputsPythonWithData(List<string> dependencyProgramNames, BsonDocument inputData)
        {
            var sb = new StringBuilder();

            // File header
            sb.AppendLine("# Auto-generated WorkflowInputs.py");
            sb.AppendLine("# This file is generated automatically from workflow dependencies");
            sb.AppendLine("# DO NOT EDIT MANUALLY - Changes will be overwritten");
            sb.AppendLine();
            sb.AppendLine("from typing import Optional, List, Dict, Any, Union");
            sb.AppendLine("import json");
            sb.AppendLine("import sys");
            sb.AppendLine();

            // WorkflowInputs class
            sb.AppendLine("class WorkflowInputs:");
            sb.AppendLine("    \"\"\"");
            sb.AppendLine("    Auto-generated workflow inputs class for easy access to dependency outputs");
            sb.AppendLine("    Provides programmatic access to all dependency program results");
            sb.AppendLine("    \"\"\"");
            sb.AppendLine();

            // Initialize method with embedded data
            sb.AppendLine("    def __init__(self):");
            
            // Embed the actual input data directly in the code
            var jsonData = inputData.ToJson();
            sb.AppendLine($"        self._inputs = {ConvertBsonToPythonDict(inputData)}");
            sb.AppendLine("        self._metadata = {");
            sb.AppendLine($"            'dependencies': {JsonSerializer.Serialize(dependencyProgramNames)},");
            sb.AppendLine($"            'generated_at': '{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC'");
            sb.AppendLine("        }");
            sb.AppendLine("        # Data is pre-loaded and ready to use - no parameters needed!");
            sb.AppendLine();

            // Generate properties for each dependency program
            foreach (var programName in dependencyProgramNames)
            {
                var propertyName = ToPythonPropertyName(programName);
                sb.AppendLine($"    @property");
                sb.AppendLine($"    def {propertyName}(self) -> Dict[str, Any]:");
                sb.AppendLine($"        \"\"\"");
                sb.AppendLine($"        Access output from {programName} program");
                sb.AppendLine($"        Returns complete execution result including stdout, stderr, exitCode, success, etc.");
                sb.AppendLine($"        \"\"\"");
                sb.AppendLine($"        return self._inputs.get('{programName}', {{}})");
                sb.AppendLine();
            }

            // Helper methods
            GenerateHelperMethods(sb, dependencyProgramNames);

            // Node access helper class
            GenerateNodeAccessHelper(sb, dependencyProgramNames);

            // Global instances
            sb.AppendLine();
            sb.AppendLine("# Global workflow inputs instance");
            sb.AppendLine("inputs = WorkflowInputs()");
            sb.AppendLine();
            sb.AppendLine("# Global nodes access helper");
            sb.AppendLine("nodes = NodeAccessHelper(inputs)");

            return sb.ToString();
        }

        public static string GenerateWorkflowInputsPython(List<string> dependencyProgramNames)
        {
            var sb = new StringBuilder();

            // File header
            sb.AppendLine("# Auto-generated WorkflowInputs.py");
            sb.AppendLine("# This file is generated automatically from workflow dependencies");
            sb.AppendLine("# DO NOT EDIT MANUALLY - Changes will be overwritten");
            sb.AppendLine();
            sb.AppendLine("from typing import Optional, List, Dict, Any, Union");
            sb.AppendLine("import json");
            sb.AppendLine("import sys");
            sb.AppendLine();

            // WorkflowInputs class
            sb.AppendLine("class WorkflowInputs:");
            sb.AppendLine("    \"\"\"");
            sb.AppendLine("    Auto-generated workflow inputs class for easy access to dependency outputs");
            sb.AppendLine("    Provides programmatic access to all dependency program results");
            sb.AppendLine("    \"\"\"");
            sb.AppendLine();

            // Initialize method
            sb.AppendLine("    def __init__(self):");
            sb.AppendLine("        self._inputs = {}");
            sb.AppendLine("        self._metadata = {");
            sb.AppendLine($"            'dependencies': {JsonSerializer.Serialize(dependencyProgramNames)},");
            sb.AppendLine($"            'generated_at': '{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC'");
            sb.AppendLine("        }");
            sb.AppendLine("        if len(sys.argv) > 1:");
            sb.AppendLine("            self.load_from_json(sys.argv[1])");
            sb.AppendLine();

            // Generate properties for each dependency program
            foreach (var programName in dependencyProgramNames)
            {
                var propertyName = ToPythonPropertyName(programName);
                sb.AppendLine($"    @property");
                sb.AppendLine($"    def {propertyName}(self) -> Dict[str, Any]:");
                sb.AppendLine($"        \"\"\"");
                sb.AppendLine($"        Access output from {programName} program");
                sb.AppendLine($"        Returns complete execution result including stdout, stderr, exitCode, success, etc.");
                sb.AppendLine($"        \"\"\"");
                sb.AppendLine($"        return self._inputs.get('{programName}', {{}})");
                sb.AppendLine();
            }

            // Helper methods
            GenerateHelperMethods(sb, dependencyProgramNames);

            // Node access helper class
            GenerateNodeAccessHelper(sb, dependencyProgramNames);

            // Global instances
            sb.AppendLine();
            sb.AppendLine("# Global workflow inputs instance");
            sb.AppendLine("inputs = WorkflowInputs()");
            sb.AppendLine();
            sb.AppendLine("# Global nodes access helper");
            sb.AppendLine("nodes = NodeAccessHelper(inputs)");

            return sb.ToString();
        }

        private static void GenerateHelperMethods(StringBuilder sb, List<string> dependencyProgramNames)
        {
            // Get stdout from specific program
            sb.AppendLine("    def get_stdout(self, program_name: str) -> str:");
            sb.AppendLine("        \"\"\"Get stdout output from a specific program\"\"\"");
            sb.AppendLine("        return self._inputs.get(program_name, {}).get('stdout', '')");
            sb.AppendLine();

            // Get stderr from specific program
            sb.AppendLine("    def get_stderr(self, program_name: str) -> str:");
            sb.AppendLine("        \"\"\"Get stderr output from a specific program\"\"\"");
            sb.AppendLine("        return self._inputs.get(program_name, {}).get('stderr', '')");
            sb.AppendLine();

            // Get exit code from specific program
            sb.AppendLine("    def get_exit_code(self, program_name: str) -> int:");
            sb.AppendLine("        \"\"\"Get exit code from a specific program\"\"\"");
            sb.AppendLine("        return self._inputs.get(program_name, {}).get('exitCode', -1)");
            sb.AppendLine();

            // Check if program succeeded
            sb.AppendLine("    def is_success(self, program_name: str) -> bool:");
            sb.AppendLine("        \"\"\"Check if a specific program executed successfully\"\"\"");
            sb.AppendLine("        return self._inputs.get(program_name, {}).get('success', False)");
            sb.AppendLine();

            // Get output files from specific program
            sb.AppendLine("    def get_output_files(self, program_name: str) -> List[str]:");
            sb.AppendLine("        \"\"\"Get list of output files from a specific program\"\"\"");
            sb.AppendLine("        return self._inputs.get(program_name, {}).get('outputFiles', [])");
            sb.AppendLine();

            // Get duration from specific program
            sb.AppendLine("    def get_duration(self, program_name: str) -> str:");
            sb.AppendLine("        \"\"\"Get execution duration from a specific program\"\"\"");
            sb.AppendLine("        return self._inputs.get(program_name, {}).get('duration', '')");
            sb.AppendLine();

            // Get all output files from all programs
            sb.AppendLine("    def get_all_output_files(self) -> List[str]:");
            sb.AppendLine("        \"\"\"Get all output files from all dependency programs\"\"\"");
            sb.AppendLine("        files = []");
            sb.AppendLine("        for program_data in self._inputs.values():");
            sb.AppendLine("            files.extend(program_data.get('outputFiles', []))");
            sb.AppendLine("        return files");
            sb.AppendLine();

            // Get successful programs
            sb.AppendLine("    def get_successful_programs(self) -> List[str]:");
            sb.AppendLine("        \"\"\"Get list of programs that executed successfully\"\"\"");
            sb.AppendLine("        return [name for name, data in self._inputs.items() if data.get('success', False)]");
            sb.AppendLine();

            // Get failed programs
            sb.AppendLine("    def get_failed_programs(self) -> List[str]:");
            sb.AppendLine("        \"\"\"Get list of programs that failed execution\"\"\"");
            sb.AppendLine("        return [name for name, data in self._inputs.items() if not data.get('success', True)]");
            sb.AppendLine();

            // Check if any program failed
            sb.AppendLine("    def has_failures(self) -> bool:");
            sb.AppendLine("        \"\"\"Check if any dependency program failed\"\"\"");
            sb.AppendLine("        return len(self.get_failed_programs()) > 0");
            sb.AppendLine();

            // Get program by name
            sb.AppendLine("    def get_program_output(self, program_name: str) -> Dict[str, Any]:");
            sb.AppendLine("        \"\"\"Get complete output from a specific program\"\"\"");
            sb.AppendLine("        return self._inputs.get(program_name, {})");
            sb.AppendLine();

            // List available programs
            sb.AppendLine("    def list_programs(self) -> List[str]:");
            sb.AppendLine("        \"\"\"Get list of all available dependency programs\"\"\"");
            sb.AppendLine("        return list(self._inputs.keys())");
            sb.AppendLine();

            // Get custom field from program output
            sb.AppendLine("    def get_custom_field(self, program_name: str, field_name: str, default: Any = None) -> Any:");
            sb.AppendLine("        \"\"\"Get a custom field from program output\"\"\"");
            sb.AppendLine("        return self._inputs.get(program_name, {}).get(field_name, default)");
            sb.AppendLine();

            // Load from JSON method
            sb.AppendLine("    def load_from_json(self, json_str: str):");
            sb.AppendLine("        \"\"\"Load dependency outputs from JSON string\"\"\"");
            sb.AppendLine("        try:");
            sb.AppendLine("            self._inputs = json.loads(json_str)");
            sb.AppendLine("        except json.JSONDecodeError as e:");
            sb.AppendLine("            print(f'Error parsing workflow inputs: {e}', file=sys.stderr)");
            sb.AppendLine("            self._inputs = {}");
            sb.AppendLine();

            // To JSON method
            sb.AppendLine("    def to_json(self) -> str:");
            sb.AppendLine("        \"\"\"Convert all inputs to JSON string\"\"\"");
            sb.AppendLine("        return json.dumps(self._inputs, default=str, indent=2)");
            sb.AppendLine();

            // Print summary method
            sb.AppendLine("    def print_summary(self):");
            sb.AppendLine("        \"\"\"Print a summary of all dependency programs and their status\"\"\"");
            sb.AppendLine("        print('=== Workflow Dependencies Summary ===')");
            sb.AppendLine("        print(f'Total programs: {len(self._inputs)}')");
            sb.AppendLine("        print(f'Successful: {len(self.get_successful_programs())}')");
            sb.AppendLine("        print(f'Failed: {len(self.get_failed_programs())}')");
            sb.AppendLine("        print()");
            sb.AppendLine("        for program_name, data in self._inputs.items():");
            sb.AppendLine("            status = '✓' if data.get('success', False) else '✗'");
            sb.AppendLine("            duration = data.get('duration', 'N/A')");
            sb.AppendLine("            file_count = len(data.get('outputFiles', []))");
            sb.AppendLine("            print(f'{status} {program_name} - Duration: {duration}, Files: {file_count}')");
            sb.AppendLine();

            // Validate inputs method
            sb.AppendLine("    def validate_required_programs(self, required_programs: List[str]) -> List[str]:");
            sb.AppendLine("        \"\"\"Validate that all required programs are present and successful\"\"\"");
            sb.AppendLine("        missing = []");
            sb.AppendLine("        for program in required_programs:");
            sb.AppendLine("            if program not in self._inputs:");
            sb.AppendLine("                missing.append(f'Program {program} is missing from inputs')");
            sb.AppendLine("            elif not self.is_success(program):");
            sb.AppendLine("                missing.append(f'Program {program} failed execution')");
            sb.AppendLine("        return missing");
            sb.AppendLine();

            // String representation
            sb.AppendLine("    def __str__(self):");
            sb.AppendLine("        return f'WorkflowInputs({len(self._inputs)} dependencies)'");
            sb.AppendLine();

            // Representation
            sb.AppendLine("    def __repr__(self):");
            sb.AppendLine("        programs = list(self._inputs.keys())");
            sb.AppendLine("        return f'WorkflowInputs(programs={programs})'");
        }

        private static void GenerateNodeAccessHelper(StringBuilder sb, List<string> dependencyProgramNames)
        {
            sb.AppendLine();
            sb.AppendLine("class WorkflowNode:");
            sb.AppendLine("    \"\"\"");
            sb.AppendLine("    Represents a single workflow node with easy access to its outputs");
            sb.AppendLine("    \"\"\"");
            sb.AppendLine();
            sb.AppendLine("    def __init__(self, name: str, data: Dict[str, Any]):");
            sb.AppendLine("        self.name = name");
            sb.AppendLine("        self._data = data");
            sb.AppendLine();

            // Generate convenient properties for common output fields
            sb.AppendLine("    @property");
            sb.AppendLine("    def stdout(self) -> str:");
            sb.AppendLine("        \"\"\"Get stdout output from this node\"\"\"");
            sb.AppendLine("        return self._data.get('stdout', '')");
            sb.AppendLine();

            sb.AppendLine("    @property");
            sb.AppendLine("    def stderr(self) -> str:");
            sb.AppendLine("        \"\"\"Get stderr output from this node\"\"\"");
            sb.AppendLine("        return self._data.get('stderr', '')");
            sb.AppendLine();

            sb.AppendLine("    @property");
            sb.AppendLine("    def print(self) -> str:");
            sb.AppendLine("        \"\"\"Get print/stdout output from this node (alias for stdout)\"\"\"");
            sb.AppendLine("        return self.stdout");
            sb.AppendLine();

            sb.AppendLine("    @property");
            sb.AppendLine("    def exit_code(self) -> int:");
            sb.AppendLine("        \"\"\"Get exit code from this node\"\"\"");
            sb.AppendLine("        return self._data.get('exitCode', -1)");
            sb.AppendLine();

            sb.AppendLine("    @property");
            sb.AppendLine("    def success(self) -> bool:");
            sb.AppendLine("        \"\"\"Check if this node executed successfully\"\"\"");
            sb.AppendLine("        return self._data.get('success', False)");
            sb.AppendLine();

            sb.AppendLine("    @property");
            sb.AppendLine("    def duration(self) -> str:");
            sb.AppendLine("        \"\"\"Get execution duration from this node\"\"\"");
            sb.AppendLine("        return self._data.get('duration', '')");
            sb.AppendLine();

            sb.AppendLine("    @property");
            sb.AppendLine("    def files(self) -> List['WorkflowFile']:");
            sb.AppendLine("        \"\"\"Get list of output files from this node\"\"\"");
            sb.AppendLine("        output_files = self._data.get('outputFiles', [])");
            sb.AppendLine("        return [WorkflowFile(f) for f in output_files]");
            sb.AppendLine();

            sb.AppendLine("    def get(self, key: str, default: Any = None) -> Any:");
            sb.AppendLine("        \"\"\"Get a custom field from this node's output\"\"\"");
            sb.AppendLine("        return self._data.get(key, default)");
            sb.AppendLine();

            sb.AppendLine("    def __str__(self):");
            sb.AppendLine("        status = '✓' if self.success else '✗'");
            sb.AppendLine("        return f'WorkflowNode({self.name}) [{status}]'");
            sb.AppendLine();

            sb.AppendLine("    def __repr__(self):");
            sb.AppendLine("        return f'WorkflowNode(name=\"{self.name}\", success={self.success})'");
            sb.AppendLine();

            // WorkflowFile helper class
            sb.AppendLine();
            sb.AppendLine("class WorkflowFile:");
            sb.AppendLine("    \"\"\"");
            sb.AppendLine("    Represents a file output from a workflow node");
            sb.AppendLine("    \"\"\"");
            sb.AppendLine();
            sb.AppendLine("    def __init__(self, file_path: str):");
            sb.AppendLine("        self._path = file_path");
            sb.AppendLine();

            sb.AppendLine("    @property");
            sb.AppendLine("    def path(self) -> str:");
            sb.AppendLine("        \"\"\"Get the full file path\"\"\"");
            sb.AppendLine("        return self._path");
            sb.AppendLine();

            sb.AppendLine("    @property");
            sb.AppendLine("    def name(self) -> str:");
            sb.AppendLine("        \"\"\"Get just the filename\"\"\"");
            sb.AppendLine("        import os");
            sb.AppendLine("        return os.path.basename(self._path)");
            sb.AppendLine();

            sb.AppendLine("    @property");
            sb.AppendLine("    def extension(self) -> str:");
            sb.AppendLine("        \"\"\"Get the file extension\"\"\"");
            sb.AppendLine("        import os");
            sb.AppendLine("        return os.path.splitext(self._path)[1]");
            sb.AppendLine();

            sb.AppendLine("    def exists(self) -> bool:");
            sb.AppendLine("        \"\"\"Check if the file exists\"\"\"");
            sb.AppendLine("        import os");
            sb.AppendLine("        return os.path.exists(self._path)");
            sb.AppendLine();

            sb.AppendLine("    def read_text(self, encoding: str = 'utf-8') -> str:");
            sb.AppendLine("        \"\"\"Read file contents as text\"\"\"");
            sb.AppendLine("        try:");
            sb.AppendLine("            with open(self._path, 'r', encoding=encoding) as f:");
            sb.AppendLine("                return f.read()");
            sb.AppendLine("        except Exception as e:");
            sb.AppendLine("            print(f'Error reading file {self._path}: {e}', file=sys.stderr)");
            sb.AppendLine("            return ''");
            sb.AppendLine();

            sb.AppendLine("    def __str__(self):");
            sb.AppendLine("        return self.name");
            sb.AppendLine();

            sb.AppendLine("    def __repr__(self):");
            sb.AppendLine("        return f'WorkflowFile(\"{self._path}\")'");
            sb.AppendLine();

            // NodeAccessHelper class
            sb.AppendLine();
            sb.AppendLine("class NodeAccessHelper:");
            sb.AppendLine("    \"\"\"");
            sb.AppendLine("    Helper class for easy node access using nodes.get_node('NodeName')");
            sb.AppendLine("    \"\"\"");
            sb.AppendLine();
            sb.AppendLine("    def __init__(self, workflow_inputs: WorkflowInputs):");
            sb.AppendLine("        self._inputs = workflow_inputs");
            sb.AppendLine();

            sb.AppendLine("    def get_node(self, program_name: str) -> WorkflowNode:");
            sb.AppendLine("        \"\"\"");
            sb.AppendLine("        Get a workflow node by program name");
            sb.AppendLine("        Returns a WorkflowNode object for easy access to outputs");
            sb.AppendLine("        \"\"\"");
            sb.AppendLine("        node_data = self._inputs.get_program_output(program_name)");
            sb.AppendLine("        return WorkflowNode(program_name, node_data)");
            sb.AppendLine();

            sb.AppendLine("    def list_nodes(self) -> List[str]:");
            sb.AppendLine("        \"\"\"Get list of all available node names\"\"\"");
            sb.AppendLine("        return self._inputs.list_programs()");
            sb.AppendLine();

            sb.AppendLine("    def get_successful_nodes(self) -> List[WorkflowNode]:");
            sb.AppendLine("        \"\"\"Get all nodes that executed successfully\"\"\"");
            sb.AppendLine("        successful_programs = self._inputs.get_successful_programs()");
            sb.AppendLine("        return [self.get_node(name) for name in successful_programs]");
            sb.AppendLine();

            sb.AppendLine("    def get_failed_nodes(self) -> List[WorkflowNode]:");
            sb.AppendLine("        \"\"\"Get all nodes that failed execution\"\"\"");
            sb.AppendLine("        failed_programs = self._inputs.get_failed_programs()");
            sb.AppendLine("        return [self.get_node(name) for name in failed_programs]");
            sb.AppendLine();

            sb.AppendLine("    def __getitem__(self, program_name: str) -> WorkflowNode:");
            sb.AppendLine("        \"\"\"Allow nodes['ProgramName'] syntax\"\"\"");
            sb.AppendLine("        return self.get_node(program_name)");
            sb.AppendLine();

            sb.AppendLine("    def __iter__(self):");
            sb.AppendLine("        \"\"\"Allow iteration over all nodes\"\"\"");
            sb.AppendLine("        for program_name in self._inputs.list_programs():");
            sb.AppendLine("            yield self.get_node(program_name)");
            sb.AppendLine();

            sb.AppendLine("    def __len__(self) -> int:");
            sb.AppendLine("        \"\"\"Get number of available nodes\"\"\"");
            sb.AppendLine("        return len(self._inputs.list_programs())");
            sb.AppendLine();

            sb.AppendLine("    def __str__(self):");
            sb.AppendLine("        return f'NodeAccessHelper({len(self)} nodes available)'");
            sb.AppendLine();

            sb.AppendLine("    def __repr__(self):");
            sb.AppendLine("        nodes = self.list_nodes()");
            sb.AppendLine("        return f'NodeAccessHelper(nodes={nodes})'");
        }

        private static string ToPythonPropertyName(string programName)
        {
            // Convert program name to a valid Python property name
            var result = new StringBuilder();
            bool nextUpper = false;

            foreach (char c in programName)
            {
                if (char.IsLetterOrDigit(c))
                {
                    result.Append(nextUpper ? char.ToUpper(c) : char.ToLower(c));
                    nextUpper = false;
                }
                else
                {
                    nextUpper = result.Length > 0; // Don't capitalize the first character
                }
            }

            var propertyName = result.ToString();
            
            // Ensure it doesn't start with a digit
            if (propertyName.Length > 0 && char.IsDigit(propertyName[0]))
            {
                propertyName = "program" + propertyName;
            }

            // Handle empty or invalid names
            if (string.IsNullOrEmpty(propertyName))
            {
                propertyName = "unknownProgram";
            }

            return propertyName;
        }

        public static string GenerateWorkflowInputsFromBsonDocument(BsonDocument inputData)
        {
            var dependencyPrograms = new List<string>();
            
            // Extract program names from the input data
            foreach (var element in inputData.Elements)
            {
                // Skip legacy fields (static inputs, user inputs, etc.)
                if (!IsLegacyField(element.Name))
                {
                    dependencyPrograms.Add(element.Name);
                }
            }

            return GenerateWorkflowInputsPythonWithData(dependencyPrograms, inputData);
        }

        private static string ConvertBsonToPythonDict(BsonDocument bsonDoc)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            
            bool first = true;
            foreach (var element in bsonDoc.Elements)
            {
                if (!IsLegacyField(element.Name))
                {
                    if (!first) sb.Append(", ");
                    first = false;
                    
                    sb.Append($"'{element.Name}': ");
                    sb.Append(ConvertBsonValueToPython(element.Value));
                }
            }
            
            sb.Append("}");
            return sb.ToString();
        }

        private static string ConvertBsonValueToPython(BsonValue value)
        {
            return value.BsonType switch
            {
                BsonType.Document => ConvertBsonDocumentToPython(value.AsBsonDocument),
                BsonType.Array => ConvertBsonArrayToPython(value.AsBsonArray),
                BsonType.String => $"'{EscapePythonString(value.AsString)}'",
                BsonType.Int32 => value.AsInt32.ToString(),
                BsonType.Int64 => value.AsInt64.ToString(),
                BsonType.Double => value.AsDouble.ToString(),
                BsonType.Boolean => value.AsBoolean ? "True" : "False",
                BsonType.Null => "None",
                BsonType.DateTime => $"'{value.AsDateTime.ToString("yyyy-MM-dd HH:mm:ss")}'",
                BsonType.ObjectId => $"'{value.AsObjectId.ToString()}'",
                _ => $"'{EscapePythonString(value.ToString())}'"
            };
        }

        private static string ConvertBsonDocumentToPython(BsonDocument doc)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            
            bool first = true;
            foreach (var element in doc.Elements)
            {
                if (!first) sb.Append(", ");
                first = false;
                
                sb.Append($"'{element.Name}': ");
                sb.Append(ConvertBsonValueToPython(element.Value));
            }
            
            sb.Append("}");
            return sb.ToString();
        }

        private static string ConvertBsonArrayToPython(BsonArray array)
        {
            var sb = new StringBuilder();
            sb.Append("[");
            
            bool first = true;
            foreach (var item in array)
            {
                if (!first) sb.Append(", ");
                first = false;
                
                sb.Append(ConvertBsonValueToPython(item));
            }
            
            sb.Append("]");
            return sb.ToString();
        }

        private static string EscapePythonString(string str)
        {
            return str.Replace("\\", "\\\\")
                     .Replace("'", "\\'")
                     .Replace("\"", "\\\"")
                     .Replace("\n", "\\n")
                     .Replace("\r", "\\r")
                     .Replace("\t", "\\t");
        }

        private static bool IsLegacyField(string fieldName)
        {
            // Common legacy field patterns that are not program names
            var legacyPatterns = new[]
            {
                "staticInput",
                "userInput", 
                "inputMapping",
                "_",
                "workflow",
                "execution"
            };

            return legacyPatterns.Any(pattern => 
                fieldName.StartsWith(pattern, StringComparison.OrdinalIgnoreCase));
        }
    }
}