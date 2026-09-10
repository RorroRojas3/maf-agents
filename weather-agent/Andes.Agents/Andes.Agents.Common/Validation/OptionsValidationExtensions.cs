using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Andes.Agents.Common.Validation;

/// <summary>Wires FluentValidation into the options pipeline.</summary>
public static class OptionsValidationExtensions
{
    /// <summary>Validates the bound options with their registered validator, failing startup when invalid.</summary>
    /// <remarks>The matching <c>IValidator&lt;TOptions&gt;</c> must be registered separately, or resolution throws at startup.</remarks>
    public static OptionsBuilder<TOptions> ValidateWithFluentValidation<TOptions>(this OptionsBuilder<TOptions> builder)
        where TOptions : class
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddSingleton<IValidateOptions<TOptions>>(
            provider => new FluentValidateOptions<TOptions>(provider, builder.Name));

        return builder;
    }
}
