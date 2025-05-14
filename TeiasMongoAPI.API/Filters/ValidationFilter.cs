using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TeiasMongoAPI.Services.DTOs.Response.Common;

namespace TeiasMongoAPI.API.Filters
{
    public class ValidationFilter : IAsyncActionFilter
    {
        private readonly IServiceProvider _serviceProvider;

        public ValidationFilter(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (!context.ActionArguments.Any())
            {
                await next();
                return;
            }

            var dtoParameter = context.ActionArguments
                .FirstOrDefault(arg => arg.Value != null &&
                    arg.Value.GetType().Namespace?.StartsWith("TeiasMongoAPI.Services.DTOs.Request") == true);

            if (dtoParameter.Value == null)
            {
                await next();
                return;
            }

            var dtoType = dtoParameter.Value.GetType();
            var validatorType = typeof(IValidator<>).MakeGenericType(dtoType);
            var validator = _serviceProvider.GetService(validatorType) as IValidator;

            if (validator == null)
            {
                await next();
                return;
            }

            var validationContext = new ValidationContext<object>(dtoParameter.Value);
            var validationResult = await validator.ValidateAsync(validationContext);

            if (!validationResult.IsValid)
            {
                var errors = new ValidationErrorResponse();

                foreach (var error in validationResult.Errors)
                {
                    if (!errors.FieldErrors.ContainsKey(error.PropertyName))
                    {
                        errors.FieldErrors[error.PropertyName] = new List<string>();
                    }
                    errors.FieldErrors[error.PropertyName].Add(error.ErrorMessage);
                }

                context.Result = new BadRequestObjectResult(errors);
                return;
            }

            await next();
        }
    }

    // Extension method to add the filter globally
    public static class ValidationFilterExtensions
    {
        public static IServiceCollection AddValidationFilter(this IServiceCollection services)
        {
            services.AddControllers(options =>
            {
                options.Filters.Add(typeof(ValidationFilter));
            });

            return services;
        }
    }
}