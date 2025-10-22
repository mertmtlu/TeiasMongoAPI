using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;
using Serilog;
using System.Text;
using TeiasMongoAPI.API.Configuration;
using TeiasMongoAPI.API.Filters;
using TeiasMongoAPI.API.Middleware;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.Configuration;
using TeiasMongoAPI.Data.Configuration;
using TeiasMongoAPI.Data.Context;
using TeiasMongoAPI.Data.Repositories;
using TeiasMongoAPI.Data.Repositories.Implementations;
using TeiasMongoAPI.Data.Services; // MODIFICATION: Added for MongoDB index initializer
using TeiasMongoAPI.Services.Interfaces;
using TeiasMongoAPI.Services.Interfaces.Execution;
using TeiasMongoAPI.Services.Mappings;
using TeiasMongoAPI.Services.Security;
using TeiasMongoAPI.Services.Services.Implementations;
using TeiasMongoAPI.Services.Services.Implementations.Execution;
using TeiasMongoAPI.API.Hubs;
using TeiasMongoAPI.API.Services;
using MongoDB.Driver.Core.Configuration; // For CompressorConfiguration
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Compression;   // For CompressorType

namespace TeiasMongoAPI.API
{
    public class Program
    {
        public static async Task Main(string[] args) // MODIFICATION: Changed to async Task
        {
            // Load environment variables from .env file
            DotNetEnv.Env.Load();

            var builder = WebApplication.CreateBuilder(args);

            // MODIFICATION: Configure Serilog with async sinks for better performance
            builder.Host.UseSerilog((context, configuration) =>
            {
                configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .WriteTo.Async(a => a.Console(outputTemplate: 
                        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"))
                    .WriteTo.Async(a => a.File(
                        path: "logs/log-.txt",
                        rollingInterval: Serilog.RollingInterval.Day,
                        rollOnFileSizeLimit: true,
                        fileSizeLimitBytes: 10485760,
                        retainedFileCountLimit: 30,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] ({SourceContext}) {Message:lj}{NewLine}{Exception}"
                        ), bufferSize: 65536) // MODIFICATION: Added buffer for better async performance
                    .Enrich.FromLogContext()
                    .Enrich.WithThreadId()
                    .Enrich.WithMachineName();
            });

            // Add services to the container.
            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                });

            // Configure API Explorer
            builder.Services.AddEndpointsApiExplorer();

            builder.Services.AddSignalR();

            // Configure Swagger/OpenAPI
            builder.Services.AddSwaggerGen(c =>
            {
                c.CustomOperationIds(apiDesc =>
                {
                    var controllerName = apiDesc.ActionDescriptor.RouteValues["controller"];
                    var actionName = apiDesc.ActionDescriptor.RouteValues["action"];
                    return $"{controllerName}_{actionName}";
                });

                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Teias Mongo API",
                    Version = "v2",
                    Description = "API for managing infrastructure data and collaborative programming",
                    Contact = new OpenApiContact
                    {
                        Name = "Development Team",
                        Email = "mertmtl0109@gmail.com"
                    }
                });

                // Add JWT Authentication
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        new string[] {}
                    }
                });

                // Enhanced enum documentation
                c.SchemaFilter<EnumSchemaFilter>();

                //// Add examples for better documentation
                //c.ExampleFilters();

                // Include XML comments for better documentation
                var xmlFiles = new[]
                {
                    "TeiasMongoAPI.API.xml",
                    "TeiasMongoAPI.Services.xml",
                    "TeiasMongoAPI.Core.xml"
                };

                foreach (var xmlFile in xmlFiles)
                {
                    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                    if (File.Exists(xmlPath))
                    {
                        c.IncludeXmlComments(xmlPath);
                    }
                }

                // Generate detailed schemas
                //c.UseAllOfToExtendReferenceSchemas();
                //c.UseAllOfForInheritance();
                
                // Register additional types that should be included in the schema
                c.SchemaFilter<AdditionalSchemasFilter>();
            });

            // MODIFICATION: Configure MongoDB with optimized connection settings
            builder.Services.Configure<MongoDbSettings>(
                builder.Configuration.GetSection("MongoDbSettings"));
            
            builder.Services.AddSingleton<IMongoClient>(serviceProvider =>
            {
                var settings = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<MongoDbSettings>>().Value;
                var mongoUrl = MongoUrl.Create(settings.ConnectionString);
                var mongoClientSettings = MongoClientSettings.FromUrl(mongoUrl);
                
                // MODIFICATION: Optimized connection pool settings for better performance
                mongoClientSettings.MaxConnectionPoolSize = 100;                        // Maximum connections in pool
                mongoClientSettings.MinConnectionPoolSize = 10;                         // Minimum connections to maintain
                mongoClientSettings.MaxConnectionIdleTime = TimeSpan.FromMinutes(30);   // Close idle connections after 30 min
                mongoClientSettings.ConnectTimeout = TimeSpan.FromSeconds(30);          // Connection timeout
                mongoClientSettings.SocketTimeout = TimeSpan.FromSeconds(30);           // Socket timeout
                mongoClientSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(30);  // Server selection timeout
                mongoClientSettings.WaitQueueTimeout = TimeSpan.FromSeconds(10);        // Wait queue timeout
                mongoClientSettings.MaxConnectionLifeTime = TimeSpan.FromMinutes(30);   // Max connection lifetime

                // MODIFICATION: Enable compression for better network performance
                mongoClientSettings.Compressors = new[] { new CompressorConfiguration(CompressorType.ZStandard) };

                // MODIFICATION: Enable read concern for better consistency
                mongoClientSettings.ReadConcern = ReadConcern.Local;
                
                // MODIFICATION: Set write concern for better performance vs consistency balance
                mongoClientSettings.WriteConcern = WriteConcern.WMajority.With(journal: true);
                
                return new MongoClient(mongoClientSettings);
            });
            
            builder.Services.AddScoped<MongoDbContext>();

            // MODIFICATION: Register MongoDB Index Initializer
            builder.Services.AddMongoDbIndexInitializer();

            // Configure JWT
            builder.Services.Configure<JwtSettings>(
                builder.Configuration.GetSection("Jwt"));

            // Configure RefreshToken settings
            builder.Services.Configure<RefreshTokenSettings>(
                builder.Configuration.GetSection("RefreshToken"));

            // Configure DemoShowcase settings
            builder.Services.Configure<TeiasMongoAPI.Core.Configuration.DemoShowcaseSettings>(
                builder.Configuration.GetSection("DemoShowcase"));

            // Configure ProjectExecution settings (includes TieredExecution)
            builder.Services.Configure<TeiasMongoAPI.Services.Services.Implementations.Execution.ProjectExecutionSettings>(
                builder.Configuration.GetSection("ProjectExecution"));

            // Configure Authentication
            var jwtSettings = builder.Configuration.GetSection("Jwt");
            var secretKey = Encoding.ASCII.GetBytes(jwtSettings["Secret"] ?? throw new InvalidOperationException("JWT Secret not configured"));

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(secretKey),
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidateAudience = true,
                    ValidAudience = jwtSettings["Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        // Check for token in cookie first
                        var token = context.Request.Cookies["AccessToken"];
                        if (!string.IsNullOrEmpty(token))
                        {
                            context.Token = token;
                            return Task.CompletedTask;
                        }

                        // Check for token in query string for SignalR
                        var accessToken = context.Request.Query["access_token"];

                        // If the request is for our hub...
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) &&
                            (path.StartsWithSegments("/executionHub")))
                        {
                            // Read the token from the query string
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    }
                };
            });

            // Configure Authorization
            builder.Services.AddAuthorization();

            // Configure AutoMapper
            builder.Services.AddAutoMapper(typeof(UserMappingProfile).Assembly);

            // Register Repositories
            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
            builder.Services.AddScoped<IIconRepository, IconRepository>();

            // Register Security Services
            builder.Services.AddSingleton<IPasswordHashingService, PasswordHashingService>();

            // Register Business Services
            builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddScoped<IGroupService, GroupService>();
            builder.Services.AddScoped<IPermissionService, PermissionService>();
            builder.Services.AddScoped<IClientService, ClientService>();
            builder.Services.AddScoped<IRegionService, RegionService>();
            builder.Services.AddScoped<ITMService, TMService>();
            builder.Services.AddScoped<IBuildingService, BuildingService>();
            builder.Services.AddScoped<IBlockService, BlockService>();
            builder.Services.AddScoped<IAlternativeTMService, AlternativeTMService>();
            builder.Services.AddScoped<IIconService, IconService>();

            // Register Collaborative Project Services 
            builder.Services.AddScoped<IVersionService, VersionService>();
            builder.Services.AddScoped<IUiComponentService, UiComponentService>();
            builder.Services.AddScoped<IRequestService, RequestService>();
            builder.Services.AddScoped<IProgramService, ProgramService>();
            builder.Services.AddScoped<IFileStorageService, FileStorageService>();
            builder.Services.AddScoped<IExecutionService, ExecutionService>();
            builder.Services.AddSingleton<IExecutionOutputStreamingService, SignalRExecutionOutputStreamingService>();
            builder.Services.AddScoped<IDeploymentService, DeploymentService>();
            builder.Services.AddScoped<IProjectExecutionEngine, ProjectExecutionEngine>();
            builder.Services.AddScoped<IRemoteAppService, RemoteAppService>();
            builder.Services.AddScoped<IDemoShowcaseService, DemoShowcaseService>();

            builder.Services.AddScoped<IProjectLanguageRunner, CSharpProjectRunner>();
            builder.Services.AddScoped<IProjectLanguageRunner, JavaProjectRunner>();
            builder.Services.AddScoped<IProjectLanguageRunner, NodeJsProjectRunner>();
            builder.Services.AddScoped<IProjectLanguageRunner, PythonProjectRunner>();

            // Register Workflow Services
            builder.Services.AddScoped<IWorkflowService, WorkflowService>();
            builder.Services.AddScoped<IWorkflowExecutionEngine, WorkflowExecutionEngine>();
            builder.Services.AddScoped<IWorkflowValidationService, WorkflowValidationService>();
            builder.Services.AddScoped<IWorkflowNotificationService, SignalRWorkflowNotificationService>();
            builder.Services.AddScoped<IUIInteractionService, UIInteractionService>();

            // Register BSON to DTO Mapping Service
            builder.Services.AddScoped<IBsonToDtoMappingService, BsonToDtoMappingService>();

            // Register Session Manager as Singleton
            builder.Services.AddSingleton<IWorkflowSessionManager, WorkflowSessionManager>();

            // Configure AI/LLM Services
            builder.Services.Configure<TeiasMongoAPI.Services.Configuration.LLMOptions>(options =>
            {
                options.Provider = Environment.GetEnvironmentVariable("LLM_PROVIDER") ?? "Gemini";
                options.ApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? string.Empty;
                options.Model = Environment.GetEnvironmentVariable("GEMINI_MODEL") ?? "gemini-2.0-flash-exp";
                options.MaxContextTokens = int.Parse(Environment.GetEnvironmentVariable("AI_MAX_CONTEXT_TOKENS") ?? "100000");
                options.MaxResponseTokens = int.Parse(Environment.GetEnvironmentVariable("AI_MAX_RESPONSE_TOKENS") ?? "8192");
                options.Temperature = double.Parse(Environment.GetEnvironmentVariable("GEMINI_TEMPERATURE") ?? "0.7");
                options.ConversationHistoryLimit = int.Parse(Environment.GetEnvironmentVariable("AI_CONVERSATION_HISTORY_LIMIT") ?? "10");
            });

            // Configure Vector Store Options
            builder.Services.Configure<TeiasMongoAPI.Services.Configuration.VectorStoreOptions>(
                builder.Configuration.GetSection(TeiasMongoAPI.Services.Configuration.VectorStoreOptions.SectionName));

            // Register AI Services
            builder.Services.AddScoped<TeiasMongoAPI.Services.Interfaces.ILLMClient, TeiasMongoAPI.Services.Services.Implementations.AI.GeminiLLMClient>();
            builder.Services.AddScoped<TeiasMongoAPI.Services.Interfaces.IIntentClassifier, TeiasMongoAPI.Services.Services.Implementations.AI.IntentClassifier>();
            builder.Services.AddScoped<TeiasMongoAPI.Services.Interfaces.ICodeIndexer, TeiasMongoAPI.Services.Services.Implementations.AI.CodeIndexer>();

            // Register Vector Store Services (optional - only if EnableVectorSearch is true)
            var vectorStoreConfig = builder.Configuration.GetSection(TeiasMongoAPI.Services.Configuration.VectorStoreOptions.SectionName)
                .Get<TeiasMongoAPI.Services.Configuration.VectorStoreOptions>();

            if (vectorStoreConfig?.EnableVectorSearch == true)
            {
                builder.Services.AddScoped<TeiasMongoAPI.Services.Interfaces.IEmbeddingService, TeiasMongoAPI.Services.Services.Implementations.AI.GoogleEmbeddingService>();
                builder.Services.AddScoped<TeiasMongoAPI.Services.Interfaces.ICodeChunker, TeiasMongoAPI.Services.Services.Implementations.AI.CodeChunker>();
                builder.Services.AddSingleton<TeiasMongoAPI.Services.Interfaces.IVectorStore, TeiasMongoAPI.Services.Services.Implementations.AI.QdrantVectorStore>();
            }

            builder.Services.AddScoped<IAIAssistantService, TeiasMongoAPI.Services.Services.Implementations.AI.AIAssistantService>();

            // Register Background Task Queue (Singleton for shared queue)
            builder.Services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();

            // Register Background Services
            builder.Services.AddHostedService<TokenCleanupService>();
            builder.Services.AddHostedService<QueuedHostedService>();
            builder.Services.AddHostedService<StaleReservationMonitorService>(); // TIERED EXECUTION: Monitors and cleans stale reservations

            // Configure CORS
            //builder.Services.AddCors(options =>
            //{
            //    options.AddPolicy("AllowSpecificOrigins",
            //        corsPolicyBuilder =>
            //        {
            //            corsPolicyBuilder
            //                .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:3000" })
            //                .AllowAnyMethod()
            //                .AllowAnyHeader()
            //                .AllowCredentials();
            //        });
            //});

            // Configure CORS for development
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowSpecificOrigins",
                    corsPolicyBuilder =>
                    {
                        if (builder.Environment.IsDevelopment())
                        {
                            // More permissive for development
                            corsPolicyBuilder
                                .AllowAnyOrigin()
                                .AllowAnyMethod()
                                .AllowAnyHeader();
                        }
                        else
                        {
                            // Production CORS
                            corsPolicyBuilder
                                .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:3000" })
                                .AllowAnyMethod()
                                .AllowAnyHeader()
                                .AllowCredentials();
                        }
                    });
            });

            // Add Memory Cache for Rate Limiting
            builder.Services.AddMemoryCache();

            // Add validation
            builder.Services.AddValidation();

            // Add HTTP Context Accessor
            builder.Services.AddHttpContextAccessor();

            // Configure Health Checks
            builder.Services.AddHealthChecks()
                .AddMongoDb(
                    mongodbConnectionString: builder.Configuration.GetValue<string>("MongoDbSettings:ConnectionString") ?? "mongodb://localhost:27017",
                    name: "mongodb",
                    tags: new[] { "database" });

            var app = builder.Build();

            // MODIFICATION: Initialize MongoDB indexes on startup for optimal performance
            try
            {
                await app.Services.InitializeMongoDbIndexesAsync();
            }
            catch (Exception ex)
            {
                // Log but don't fail startup - indexes can be created later
                app.Logger.LogError(ex, "Failed to initialize MongoDB indexes during startup");
            }

            // Configure the HTTP request pipeline.

            // Use Serilog Request Logging
            app.UseSerilogRequestLogging();

            // Global Exception Handler
            app.UseGlobalExceptionHandler();

            // Request Logging Middleware
            app.UseRequestLogging();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Teias Mongo API v1");
                    // Keep default route prefix so Swagger is at /swagger
                    // c.RoutePrefix = string.Empty;
                });
            }
            else
            {
                app.UseHttpsRedirection();

            }

            // Static Files (for serving videos)
            app.UseStaticFiles();

            // CORS
            app.UseCors("AllowSpecificOrigins");

            // Authentication & Authorization
            app.UseAuthentication();
            app.UseTokenVersionValidation(); // Validate token version after authentication
            app.UseAuthorization();

            // Map Controllers
            app.MapControllers();

            // Map SignalR Hubs
            app.MapHub<UIWorkflowHub>("/uiWorkflowHub");
            app.MapHub<ExecutionHub>("/executionHub"); // LIVE STREAMING: Real-time execution output

            // Health Checks
            app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
            {
                ResponseWriter = async (context, report) =>
                {
                    context.Response.ContentType = "application/json";
                    var result = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        status = report.Status.ToString(),
                        checks = report.Entries.Select(entry => new
                        {
                            name = entry.Key,
                            status = entry.Value.Status.ToString(),
                            description = entry.Value.Description,
                            duration = entry.Value.Duration.ToString()
                        })
                    });
                    await context.Response.WriteAsync(result);
                }
            });

            // Run the application
            await app.RunAsync(); // MODIFICATION: Use RunAsync for consistency
        }
    }
}