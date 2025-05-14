using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace TeiasMongoAPI.Services.Validators.Configuration
{
    public static class ValidationServiceExtensions
    {
        public static IServiceCollection AddCustomValidators(this IServiceCollection services)
        {
            // Register all validators from the assembly
            services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

            // Configure validation behavior
            services.Configure<ApiBehaviorOptions>(options =>
            {
                options.SuppressModelStateInvalidFilter = true;
            });

            return services;
        }
    }
}

// In your API/Program.cs or Startup.cs, add:
// builder.Services.AddCustomValidators();