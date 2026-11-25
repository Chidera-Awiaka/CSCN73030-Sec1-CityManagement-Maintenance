using Microsoft.AspNetCore.Mvc;

namespace Maintenance.Api.Infrastructure;

/// <summary>Kind of result returned by the Sprint 3 services.</summary>
public enum OutcomeKind
{
    /// <summary>The operation succeeded.</summary>
    Success,
    /// <summary>The request was invalid (HTTP 400).</summary>
    Invalid,
    /// <summary>A record was not found (HTTP 404).</summary>
    NotFound,
    /// <summary>The operation conflicts with the current state (HTTP 409).</summary>
    Conflict,
    /// <summary>The caller is not allowed to do this (HTTP 403).</summary>
    Forbidden,
    /// <summary>An external service could not be reached (HTTP 502).</summary>
    ExternalFailure
}

/// <summary>Result of a service operation: kind, value and messages.</summary>
/// <typeparam name="T">Value type.</typeparam>
/// <param name="Kind">What happened.</param>
/// <param name="Value">The record involved, when there is one.</param>
/// <param name="Errors">Messages explaining a failure (or warnings on success).</param>
/// <param name="Duplicate">True when an inbound message had already been received (TEC-04).</param>
public record ServiceOutcome<T>(OutcomeKind Kind, T? Value, List<string> Errors, bool Duplicate = false)
{
    /// <summary>Successful result.</summary>
    public static ServiceOutcome<T> Ok(T value, bool duplicate = false) => new(OutcomeKind.Success, value, new List<string>(), duplicate);

    /// <summary>Failed result.</summary>
    public static ServiceOutcome<T> Fail(OutcomeKind kind, params string[] errors) => new(kind, default, errors.ToList());

    /// <summary>Failed result with a list of errors.</summary>
    public static ServiceOutcome<T> Fail(OutcomeKind kind, List<string> errors) => new(kind, default, errors);

    /// <summary>True when the operation succeeded.</summary>
    public bool IsSuccess => Kind == OutcomeKind.Success;
}

/// <summary>Turns a <see cref="ServiceOutcome{T}"/> into an HTTP result.</summary>
public static class OutcomeResults
{
    /// <summary>
    /// 200 with the value on success (or the given success result); otherwise
    /// 400 / 404 / 409 / 403 / 502 with { errors }.
    /// </summary>
    public static IActionResult ToResult<T>(this ControllerBase controller, ServiceOutcome<T> outcome, Func<T, IActionResult>? onSuccess = null)
    {
        if (outcome.IsSuccess)
        {
            return onSuccess is not null ? onSuccess(outcome.Value!) : controller.Ok(outcome.Value);
        }

        var body = new { errors = outcome.Errors };
        return outcome.Kind switch
        {
            OutcomeKind.NotFound => controller.NotFound(body),
            OutcomeKind.Conflict => controller.Conflict(body),
            OutcomeKind.Forbidden => controller.StatusCode(StatusCodes.Status403Forbidden, body),
            OutcomeKind.ExternalFailure => controller.StatusCode(StatusCodes.Status502BadGateway, body),
            _ => controller.BadRequest(body)
        };
    }
}
