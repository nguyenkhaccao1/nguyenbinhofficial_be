using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NguyenBinh.Shared.Results;

namespace NguyenBinh.Api.Infrastructure;

/// <summary>Chay FluentValidation cho moi tham so action co validator dang ky → 422 kem loi theo field.</summary>
internal sealed class ValidationFilter(IServiceProvider services) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var failures = new List<ValidationFailure>();
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null) continue;
            var validator = (IValidator?)services.GetService(typeof(IValidator<>).MakeGenericType(argument.GetType()));
            if (validator is null) continue;

            var result = await validator.ValidateAsync(new ValidationContext<object>(argument),
                context.HttpContext.RequestAborted);
            failures.AddRange(result.Errors);
        }

        if (failures.Count > 0)
        {
            context.Result = new ObjectResult(ApiResponse.Fail("Dữ liệu không hợp lệ.", ValidationErrors.From(failures)))
            {
                StatusCode = StatusCodes.Status422UnprocessableEntity,
            };
            return;
        }

        await next();
    }
}

internal static class ValidationErrors
{
    /// <summary>"Tags[0]" → "tags[0]", "Address.City" → "address.city" de khop ten field camelCase o frontend.</summary>
    public static IReadOnlyDictionary<string, string[]> From(IEnumerable<ValidationFailure> failures) =>
        failures.GroupBy(f => CamelCasePath(f.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).Distinct().ToArray());

    private static string CamelCasePath(string path) =>
        string.Join('.', path.Split('.').Select(s => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..]));
}
