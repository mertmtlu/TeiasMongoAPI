using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using TeiasMongoAPI.API.Filters;

namespace TeiasMongoAPI.API.Configuration
{
    public static class ValidationConfiguration
    {
        public static IServiceCollection AddValidation(this IServiceCollection services)
        {
            // Add FluentValidation validators from the Services assembly
            services.AddValidatorsFromAssembly(typeof(TeiasMongoAPI.Services.Validators.Auth.UserRegisterDtoValidator).Assembly);

            // Configure API behavior
            services.Configure<ApiBehaviorOptions>(options =>
            {
                options.SuppressModelStateInvalidFilter = true;
            });

            // Add custom validation filter
            services.AddControllers(options =>
            {
                options.Filters.Add(typeof(ValidationFilter));
            });

            return services;
        }
    }
}