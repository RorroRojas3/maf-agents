using FluentValidation;
using FluentValidation.Results;

namespace Andes.Agents.Api.Filters;

/// <summary>Validates a bound argument before the handler runs.</summary>
internal static class ValidationEndpointFilter
{
    /// <summary>
    /// Validates the first argument of type <typeparamref name="TArgument"/> with its registered validator.
    /// </summary>
    /// <remarks>
    /// Minimal APIs do not validate <c>[AsParameters]</c> records on their own, so a query-parameter shape is
    /// only checked where this filter is attached.
    /// </remarks>
    public static Func<EndpointFilterInvocationContext, EndpointFilterDelegate, ValueTask<object?>> Require<TArgument>()
        where TArgument : class
    {
        return async (context, next) =>
        {
            TArgument? argument = context.Arguments.OfType<TArgument>().FirstOrDefault();

            if (argument is not null)
            {
                IValidator<TArgument> validator = context.HttpContext.RequestServices.GetRequiredService<IValidator<TArgument>>();
                ValidationResult result = await validator.ValidateAsync(argument, context.HttpContext.RequestAborted);

                if (!result.IsValid)
                {
                    throw new ValidationException(result.Errors);
                }
            }

            return await next(context);
        };
    }
}
