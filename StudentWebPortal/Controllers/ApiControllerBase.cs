using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace StudentWebPortal.Controllers;

/// <summary>
/// Shared behaviour for all API controllers: consistent RFC 9457 Problem Details errors,
/// pagination limits/headers, and small input-normalisation helpers.
/// </summary>
[ApiController]
[ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
public abstract class ApiControllerBase : ControllerBase
{
    protected const int MaxPage = 1_000_000;
    protected const int MaxPageSize = 100;

    protected ObjectResult BadRequestProblem(string detail) =>
        Problem(title: "Bad request", detail: detail, statusCode: StatusCodes.Status400BadRequest);

    protected ObjectResult ConflictProblem(string detail) =>
        Problem(title: "Conflict", detail: detail, statusCode: StatusCodes.Status409Conflict);

    /// <summary>Returns a 400 result when the paging arguments are out of range; otherwise null.</summary>
    protected ObjectResult? ValidatePaging(int page, int pageSize) =>
        page is >= 1 and <= MaxPage && pageSize is >= 1 and <= MaxPageSize
            ? null
            : BadRequestProblem($"page must be between 1 and {MaxPage:N0} and pageSize must be between 1 and {MaxPageSize}.");

    /// <summary>Returns a 400 result when fromDate is after toDate; otherwise null.</summary>
    protected ObjectResult? ValidateDateRange(DateTime? fromDate, DateTime? toDate) =>
        fromDate.HasValue && toDate.HasValue && fromDate.Value.Date > toDate.Value.Date
            ? BadRequestProblem("fromDate must be on or before toDate.")
            : null;

    protected void SetPagingHeaders(int totalCount, int page, int pageSize)
    {
        Response.Headers["X-Total-Count"] = totalCount.ToString();
        Response.Headers["X-Page"] = page.ToString();
        Response.Headers["X-Page-Size"] = pageSize.ToString();
    }

    /// <summary>Start of the given day, as UTC (inclusive lower bound).</summary>
    protected static DateTime StartOfDayUtc(DateTime value) => DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);

    /// <summary>Start of the day after the given day, as UTC (exclusive upper bound).</summary>
    protected static DateTime StartOfNextDayUtc(DateTime value) => DateTime.SpecifyKind(value.Date.AddDays(1), DateTimeKind.Utc);

    protected static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    protected static string? NormalizeEmail(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    protected static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
