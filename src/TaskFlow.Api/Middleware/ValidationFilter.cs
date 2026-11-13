using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;
using TaskFlow.Application.Common;

namespace TaskFlow.Api.Middleware;

/// <summary>
/// Runs the FluentValidation validator registered for each action argument (body and
/// query DTOs) before the action executes; failures become a 400 ValidationProblemDetails.
/// </summary>
public sealed class ValidationFilter(IServiceProvider services) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var errors = new Dictionary<string, List<string>>();

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null) continue;

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (services.GetService(validatorType) is not IValidator validator) continue;

            var result = await validator.ValidateAsync(new ValidationContext<object>(argument), context.HttpContext.RequestAborted);
            foreach (var failure in result.Errors)
            {
                var key = ToCamelCase(failure.PropertyName);
                if (!errors.TryGetValue(key, out var list))
                    errors[key] = list = [];
                list.Add(failure.ErrorMessage);
            }
        }

        if (errors.Count > 0)
            throw new RequestValidationException(errors.ToDictionary(e => e.Key, e => e.Value.ToArray()));

        await next();
    }

    private static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
