using MongoDB.Bson;
using System.Text;
using System.Text.Json;
using TeiasMongoAPI.Core.Models.Collaboration;

namespace TeiasMongoAPI.Services.Helpers
{
    public static class WorkflowInputsGenerator
    {
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

            // Global inputs instance
            sb.AppendLine();
            sb.AppendLine("# Global workflow inputs instance");
            sb.AppendLine("inputs = WorkflowInputs()");

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

            return GenerateWorkflowInputsPython(dependencyPrograms);
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