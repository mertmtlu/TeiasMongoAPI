using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Text;
using TeiasMongoAPI.API.Configuration;
using TeiasMongoAPI.API.Middleware;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.Configuration;
using TeiasMongoAPI.Data.Configuration;
using TeiasMongoAPI.Data.Context;
using TeiasMongoAPI.Data.Repositories;
using TeiasMongoAPI.Services.Interfaces;
using TeiasMongoAPI.Services.Mappings;
using TeiasMongoAPI.Services.Security;
using TeiasMongoAPI.Services.Services.Implementations;

namespace TeiasMongoAPI.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Configure Serilog
            builder.Host.UseSerilog((context, configuration) =>
                configuration.ReadFrom.Configuration(context.Configuration));

            // Add services to the container.
            builder.Services.AddControllers();

            // Configure API Explorer
            builder.Services.AddEndpointsApiExplorer();

            // Configure Swagger/OpenAPI
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Teias Mongo API",
                    Version = "v1",
                    Description = "API for managing Teias infrastructure data",
                    Contact = new OpenApiContact
                    {
                        Name = "Teias Team",
                        Email = "support@teias.com"
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

                // Add XML comments if available
                var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (File.Exists(xmlPath))
                {
                    c.IncludeXmlComments(xmlPath);
                }
            });

            // Configure MongoDB
            builder.Services.Configure<MongoDbSettings>(
                builder.Configuration.GetSection("MongoDbSettings"));
            builder.Services.AddSingleton<MongoDbContext>();

            // Configure JWT
            builder.Services.Configure<JwtSettings>(
                builder.Configuration.GetSection("Jwt"));

            // Configure RefreshToken settings
            builder.Services.Configure<RefreshTokenSettings>(
                builder.Configuration.GetSection("RefreshToken"));

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
                        // Check for token in cookie as well
                        var token = context.Request.Cookies["AccessToken"];
                        if (!string.IsNullOrEmpty(token))
                        {
                            context.Token = token;
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

            // Register Security Services
            builder.Services.AddSingleton<IPasswordHashingService, PasswordHashingService>();

            // Register Business Services
            builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddScoped<IClientService, ClientService>();
            builder.Services.AddScoped<IRegionService, RegionService>();
            builder.Services.AddScoped<ITMService, TMService>();
            builder.Services.AddScoped<IBuildingService, BuildingService>();
            builder.Services.AddScoped<IBlockService, BlockService>();
            builder.Services.AddScoped<IAlternativeTMService, AlternativeTMService>();

            // Register Background Services
            builder.Services.AddHostedService<TokenCleanupService>();

            // Configure CORS
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowSpecificOrigins",
                    corsPolicyBuilder =>
                    {
                        corsPolicyBuilder
                            .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:3000" })
                            .AllowAnyMethod()
                            .AllowAnyHeader()
                            .AllowCredentials();
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

            app.UseHttpsRedirection();

            // CORS
            app.UseCors("AllowSpecificOrigins");

            // Authentication & Authorization
            app.UseAuthentication();
            app.UseAuthorization();

            // Map Controllers
            app.MapControllers();

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
            app.Run();
        }
    }
}