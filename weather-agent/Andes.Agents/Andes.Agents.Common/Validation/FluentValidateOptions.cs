using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Andes.Agents.Common.Validation;

/// <summary>Validates an options instance with the <see cref="IValidator{T}"/> registered for it.</summary>
/// <typeparam name="TOptions">The options type being validated.</typeparam>
public sealed class FluentValidateOptions<TOptions>(IServiceProvider services, string? name) : IValidateOptions<TOptions>
    where TOptions : class
{
    private readonly IServiceProvider _services = services;
    private readonly string? _name = name;

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, TOptions options)
    {
        // A named registration must ignore every other name, or it would reject instances it does not own.
        if (_name is not null && _name != name)
        {
            return ValidateOptionsResult.Skip;
        }

        // Options validation resolves from the root provider; the scope is here so a future validator may
        // take scoped dependencies. Every validator today is a stateless singleton.
        using IServiceScope scope = _services.CreateScope();
        IValidator<TOptions> validator = scope.ServiceProvider.GetRequiredService<IValidator<TOptions>>();
        ValidationResult result = validator.Validate(options);

        return result.IsValid
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(result.Errors.Select(failure => $"{typeof(TOptions).Name}.{failure.PropertyName}: {failure.ErrorMessage}"));
    }
}
