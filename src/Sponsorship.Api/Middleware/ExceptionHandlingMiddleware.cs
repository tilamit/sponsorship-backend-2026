using System.Net;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Sponsorship.Application.Common.Exceptions;
using Sponsorship.Domain.Exceptions;

namespace Sponsorship.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (ValidationException vex)
        {
            await WriteValidationProblem(ctx, vex);
        }
        catch (NotFoundException nex)
        {
            await WriteProblem(ctx, HttpStatusCode.NotFound, "Not Found", nex.Message);
        }
        catch (ForbiddenException fex)
        {
            await WriteProblem(ctx, HttpStatusCode.Forbidden, "Forbidden", fex.Message);
        }
        catch (UnauthorizedException uex)
        {
            await WriteProblem(ctx, HttpStatusCode.Unauthorized, "Unauthorized", uex.Message);
        }
        catch (DomainException dex)
        {
            await WriteProblem(ctx, HttpStatusCode.BadRequest, "Bad Request", dex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteProblem(ctx, HttpStatusCode.InternalServerError,
                "Internal Server Error", "An unexpected error occurred.");
        }
    }

    private static Task WriteProblem(HttpContext ctx, HttpStatusCode status, string title, string detail)
    {
        var problem = new ProblemDetails
        {
            Status = (int)status,
            Title = title,
            Detail = detail,
            Instance = ctx.Request.Path
        };
        ctx.Response.StatusCode = (int)status;
        ctx.Response.ContentType = "application/problem+json";
        return ctx.Response.WriteAsync(JsonSerializer.Serialize(problem));
    }

    private static Task WriteValidationProblem(HttpContext ctx, ValidationException vex)
    {
        var errors = vex.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        var problem = new ValidationProblemDetails(errors)
        {
            Status = (int)HttpStatusCode.BadRequest,
            Title = "Validation failed",
            Instance = ctx.Request.Path
        };
        ctx.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        ctx.Response.ContentType = "application/problem+json";
        return ctx.Response.WriteAsync(JsonSerializer.Serialize(problem));
    }
}
