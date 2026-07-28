using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using NetWatch.Application.Common.Exceptions;
using NetWatch.Application.Common.Interfaces;
using NetWatch.Domain.Common;

namespace NetWatch.Api.Middleware;

/// <summary>
/// Turns exceptions into RFC 9457 problem responses.
///
/// One place decides the status code for every failure, so services and controllers can
/// throw meaningful exceptions instead of each returning its own error shape. Unexpected
/// exceptions are logged in full and reported as a bare 500 — stack traces are for the
/// log, not for the client.
/// </summary>
public class GlobalExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var problem = Map(exception);

        if (problem.Status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception on {Method} {Path}.", httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogInformation(
                "Request to {Method} {Path} rejected with {Status}: {Detail}",
                httpContext.Request.Method, httpContext.Request.Path, problem.Status, problem.Detail);
        }

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }

    private static ProblemDetails Map(Exception exception) => exception switch
    {
        NotFoundException notFound => new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Not found",
            Detail = notFound.Message
        },

        ConflictException conflict => new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Conflict",
            Detail = conflict.Message
        },

        AuthenticationFailedException auth => new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Authentication failed",
            Detail = auth.Message
        },

        // FluentValidation failures are grouped by property so the client can highlight
        // each offending field rather than showing one flat message.
        ValidationException validation => new ValidationProblemDetails(
            validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed"
        },

        DomainValidationException domainValidation => new ValidationProblemDetails(
            new Dictionary<string, string[]> { [string.Empty] = [.. domainValidation.Errors] })
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed"
        },

        // A domain rule was broken. The message is written for humans and safe to return.
        DomainException domain => new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid operation",
            Detail = domain.Message
        },

        _ => new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Unexpected error",
            Detail = "An unexpected error occurred. The incident has been logged."
        }
    };
}
