using FluentValidation;

namespace Andes.Agents.Api.Options;

/// <summary>How much the per-request log line records.</summary>
public sealed class RequestLoggingOptions
{
    /// <summary>Configuration section these options bind from.</summary>
    public const string SectionName = "RequestLogging";

    /// <summary>Gets or sets whether one line is logged per request.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the duration above which a request is logged as a warning instead of information.</summary>
    public int SlowRequestThresholdMilliseconds { get; set; } = 5_000;

    /// <summary>Gets or sets the path prefixes that are not logged; probes would otherwise dominate the sink.</summary>
    public string[] ExcludedPaths { get; set; } = ["/health"];
}

internal sealed class RequestLoggingOptionsValidator : AbstractValidator<RequestLoggingOptions>
{
    public RequestLoggingOptionsValidator()
    {
        RuleFor(options => options.SlowRequestThresholdMilliseconds).GreaterThan(0);

        // A prefix without its leading slash makes PathString throw on every request, and a blank one matches everything.
        RuleForEach(options => options.ExcludedPaths)
            .Must(path => path.StartsWith('/'))
            .WithMessage($"'{RequestLoggingOptions.SectionName}:{nameof(RequestLoggingOptions.ExcludedPaths)}' entries must each start with '/'.");
    }
}
