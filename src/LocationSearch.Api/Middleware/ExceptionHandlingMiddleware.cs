using System.Net;
using System.Text.Json;
using Users.Application.Common.Exceptions;

namespace LocationSearch.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    public ExceptionHandlingMiddleware(RequestDelegate next)
    {
        _next = next;
    }
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ConflictException ex)
        {
            await WriteResponse(
                context,
                HttpStatusCode.Conflict,
                ex.Message
            );
        }
        catch (UnauthorizedException ex)
        {
            await WriteResponse(
                context,
                HttpStatusCode.Unauthorized,
                ex.Message
            );
        }
        catch (Exception)
        {
            await WriteResponse(
                context,
                HttpStatusCode.InternalServerError,
                "An unexpected error occured."
            );
        }
    }
    private static async Task WriteResponse(
        HttpContext context,
        HttpStatusCode statusCode,
        string message
    )
    {
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var response = new
        {
            status = (int)statusCode,
            message
        };
        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response)
        );
    }
}