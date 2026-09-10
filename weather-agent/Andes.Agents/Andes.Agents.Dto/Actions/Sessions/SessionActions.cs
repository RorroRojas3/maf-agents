using FluentValidation;

namespace Andes.Agents.Dto.Actions.Sessions;

/// <summary>Paging parameters for listing a caller's sessions.</summary>
/// <param name="Skip">Number of sessions to skip.</param>
/// <param name="Take">Page size, at most 100.</param>
public sealed record ListSessionsActionDto(int Skip = 0, int Take = 20);

/// <summary>Paging parameters for listing the messages of a session.</summary>
/// <param name="Skip">Number of messages to skip.</param>
/// <param name="Take">Page size, at most 200.</param>
public sealed record ListSessionMessagesActionDto(int Skip = 0, int Take = 50);

/// <summary>Rules for <see cref="ListSessionsActionDto"/>.</summary>
public sealed class ListSessionsActionDtoValidator : AbstractValidator<ListSessionsActionDto>
{
    /// <summary>Creates the validator.</summary>
    public ListSessionsActionDtoValidator()
    {
        RuleFor(action => action.Skip).GreaterThanOrEqualTo(0);
        RuleFor(action => action.Take).InclusiveBetween(1, 100);
    }
}

/// <summary>Rules for <see cref="ListSessionMessagesActionDto"/>.</summary>
public sealed class ListSessionMessagesActionDtoValidator : AbstractValidator<ListSessionMessagesActionDto>
{
    /// <summary>Creates the validator.</summary>
    public ListSessionMessagesActionDtoValidator()
    {
        RuleFor(action => action.Skip).GreaterThanOrEqualTo(0);
        RuleFor(action => action.Take).InclusiveBetween(1, 200);
    }
}
