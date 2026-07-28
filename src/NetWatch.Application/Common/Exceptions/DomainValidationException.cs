namespace NetWatch.Application.Common.Exceptions;

/// <summary>
/// Carries one or more validation failures that were produced outside FluentValidation —
/// most often by ASP.NET Core Identity, whose password and email rules run inside
/// <c>UserManager</c> and surface as an <c>IdentityResult</c> rather than as model state.
/// Translated to a 400 with the same shape as any other validation error, so clients
/// only ever handle one error format.
/// </summary>
public class DomainValidationException(IEnumerable<string> errors)
    : Exception("One or more validation errors occurred.")
{
    public IReadOnlyList<string> Errors { get; } = [.. errors];
}
