// Service Registration Code for Program.cs
// Add these registrations to your Program.cs file after the existing service registrations

// Background Task Queue (Singleton - shared across all requests)
builder.Services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();

// Queued Hosted Service (Hosted Service - runs for application lifetime)
builder.Services.AddHostedService<QueuedHostedService>();

// Note: Keep the existing IWorkflowExecutionEngine registration as scoped:
// builder.Services.AddScoped<IWorkflowExecutionEngine, WorkflowExecutionEngine>();

// Complete service registration section should look like this:
/*
// ... existing services ...

// Workflow Services
builder.Services.AddScoped<IWorkflowService, WorkflowService>();
builder.Services.AddScoped<IWorkflowExecutionEngine, WorkflowExecutionEngine>();
builder.Services.AddScoped<IWorkflowValidationService, WorkflowValidationService>();

// Background Task Processing (NEW)
builder.Services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
builder.Services.AddHostedService<QueuedHostedService>();

// Session Management
builder.Services.AddScoped<IWorkflowSessionManager, WorkflowSessionManager>();

// ... rest of existing services ...
*/