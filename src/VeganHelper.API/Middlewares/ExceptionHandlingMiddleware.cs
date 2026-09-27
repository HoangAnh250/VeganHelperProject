namespace VeganHelper.API.Middlewares;

using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex, "Validation error occurred.");
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";

            var errors = ex.Errors.Select(e => new { Field = e.PropertyName, Error = e.ErrorMessage });
            var result = JsonSerializer.Serialize(new { message = "Validation failed", errors });

            await context.Response.WriteAsync(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Argument error occurred.");
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";

            var result = JsonSerializer.Serialize(new { message = ex.Message });
            await context.Response.WriteAsync(result);
        }
        catch (Microsoft.AspNetCore.Http.BadHttpRequestException ex)
        {
            _logger.LogWarning(ex, "Bad HTTP request error occurred.");
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";

            var result = JsonSerializer.Serialize(new { message = "Invalid request format or missing form data." });
            await context.Response.WriteAsync(result);
        }
        catch (System.IO.InvalidDataException ex)
        {
            _logger.LogWarning(ex, "Invalid data error occurred.");
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";

            var result = JsonSerializer.Serialize(new { message = "Invalid form data format." });
            await context.Response.WriteAsync(result);
        }
        catch (VeganHelper.BLL.Exceptions.NotFoundException ex)
        {
            _logger.LogWarning(ex, "Resource not found error occurred.");
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.ContentType = "application/json";

            var result = JsonSerializer.Serialize(new { message = ex.Message });
            await context.Response.WriteAsync(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred.");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            var result = JsonSerializer.Serialize(new { message = "An internal server error occurred.", details = ex.ToString() });
            await context.Response.WriteAsync(result);
        }
    }
}
