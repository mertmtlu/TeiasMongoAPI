# Workflow Testing Guide

## Overview

This guide helps you test the new user-managed workflow input system using two example programs that demonstrate dependency handling.

## Test Programs

### Program 1: DataGenerator
- **File**: `TestProgram1_DataGenerator.py`
- **Purpose**: Generates sample sales data (acts as source node)
- **Dependencies**: None
- **Outputs**: 
  - `sales_data.json` - Complete sales records
  - `sales_summary.json` - Summary statistics
  - Stdout with parseable metrics

### Program 2: DataAnalyzer  
- **File**: `TestProgram2_DataAnalyzer.py`
- **Purpose**: Analyzes data from DataGenerator
- **Dependencies**: DataGenerator
- **Outputs**:
  - `analysis_report.json` - Complete analysis
  - `analysis_summary.json` - Executive summary
  - Stdout with analysis results

## Step-by-Step Testing Instructions

### Step 1: Create Programs in Your System

1. **Create DataGenerator Program**:
   - Go to your program creation interface
   - Set Program Name: `DataGenerator`
   - Set Language: Python
   - Copy content from `TestProgram1_DataGenerator.py`
   - Save the program

2. **Create DataAnalyzer Program**:
   - Create another program
   - Set Program Name: `DataAnalyzer` 
   - Set Language: Python
   - Copy content from `TestProgram2_DataAnalyzer.py`
   - Save the program

### Step 2: Create Workflow

1. **Create New Workflow**:
   - Name: `Sales Data Analysis Workflow`
   - Description: `Test workflow for user-managed input system`

2. **Add DataGenerator Node**:
   - Add node using DataGenerator program
   - Node Name: `Generate Sales Data`
   - No dependencies required (source node)
   - Optional: Add static input `recordCount` with value `200`

3. **Add DataAnalyzer Node**:
   - Add node using DataAnalyzer program  
   - Node Name: `Analyze Sales Data`
   - **Important**: Create edge from DataGenerator node to this node
   - This creates the dependency relationship

### Step 3: Test Execution

1. **Execute the Workflow**:
   - Start workflow execution
   - Monitor the execution logs

2. **Expected Behavior**:

   **DataGenerator Node** should:
   - [OK] Execute successfully (no dependencies)
   - [OK] Output: "Generated 200 sales records" (or your specified count)
   - [OK] Create output files: `sales_data.json`, `sales_summary.json`
   - [OK] Show total sales amount, quantity, and average order value

   **DataAnalyzer Node** should:
   - [OK] Receive DataGenerator output via the new input system
   - [OK] Show dependency summary with DataGenerator status
   - [OK] Extract metrics from DataGenerator stdout
   - [OK] Generate insights and recommendations
   - [OK] Create analysis files: `analysis_report.json`, `analysis_summary.json`

## What to Look For

### Success Indicators

1. **Enhanced Logging**:
   ```
   Node xyz has 1 dependencies: [abc]
   Added dependency 'DataGenerator' output for node xyz
   Generated WorkflowInputs helper for node xyz with 1 dependencies
   ```

2. **DataAnalyzer Output Should Show**:
   ```
   === Workflow Dependencies Summary ===
   OK DataGenerator - Duration: 00:00:XX, Files: 2
   SUCCESS: All dependencies successful!
   Extracting Metrics from DataGenerator...
      Records Processed: 200
      Total Sales: $XX,XXX.XX
   ```

3. **Proper Data Flow**:
   - DataAnalyzer should parse DataGenerator's stdout correctly
   - Should extract record count, sales totals, etc.
   - Should generate meaningful insights based on the data

### Debug Information

If something goes wrong, check these logs:

1. **Dependency Resolution**:
   - Look for logs showing dependency node IDs found
   - Check if program names are resolved correctly

2. **Input Data Preparation**:
   - Verify that dependency outputs are being collected
   - Check that program names appear in the input data

3. **Helper Generation**:
   - Confirm WorkflowInputs helper is generated
   - Verify it's passed to the program via environment

## Advanced Testing

### Test Multiple Dependencies

1. Create a third program (e.g., `ReportGenerator`) that depends on both `DataGenerator` AND `DataAnalyzer`
2. In this program, you should be able to access both dependencies:
   ```python
   inputs.dataGenerator  # First dependency
   inputs.dataAnalyzer   # Second dependency
   ```

### Test Failure Handling

1. Modify DataGenerator to intentionally fail (e.g., `sys.exit(1)`)
2. DataAnalyzer should detect the failure:
   ```python
   if inputs.has_failures():
       print("ERROR: Some dependencies failed!")
   ```

### Test Different Program Names

1. Create programs with complex names (spaces, special characters)
2. Verify they're converted to valid Python identifiers
3. Example: `"Data Processor v2.1"` → `inputs.dataProcessorV21`

## Expected Results

After successful execution, you should have:

1. **DataGenerator Results**:
   - Sales data with 200 records (or your specified count)
   - Summary statistics showing totals and breakdowns
   - Clear, parseable stdout output

2. **DataAnalyzer Results**:
   - Successfully parsed DataGenerator metrics
   - Generated business insights based on the data
   - Created analysis and summary reports
   - Demonstrated dependency access via `inputs.dataGenerator`

3. **System Behavior**:
   - No schema validation errors
   - Clear program-name-based dependencies
   - Rich context passed between programs
   - Backward compatibility maintained

This test demonstrates the key benefits:
- [+] No complex input mappings needed
- [+] Clear program relationships (`inputs.dataGenerator`)
- [+] Rich context available (stdout, files, success status, duration)
- [+] User-controlled validation and error handling
- [+] Scales to multiple dependencies easily

## Troubleshooting

**Issue**: DataAnalyzer can't find DataGenerator dependency
- **Check**: Ensure edge exists between nodes in workflow
- **Check**: Verify DataGenerator node executed successfully
- **Check**: Look for dependency resolution logs

**Issue**: Program names not appearing correctly  
- **Check**: Program entity has correct name field
- **Check**: Look for program name resolution logs
- **Check**: Verify CleanProgramName function is working

**Issue**: No WorkflowInputs helper generated
- **Check**: Environment variable `WORKFLOW_INPUTS_HELPER` is set
- **Check**: Generator is being called during node execution
- **Check**: Dependencies are found and collected properly