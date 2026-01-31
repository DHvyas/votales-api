using System.Net;
using System.Text.Json;
using VoTales.API.Services;

namespace VoTales.API.Middleware;

public class ExceptionHandlingMiddleware : IMiddleware
{
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IEmailService _emailService;

    public ExceptionHandlingMiddleware(
        ILogger<ExceptionHandlingMiddleware> logger,
        IEmailService emailService)
    {
        _logger = logger;
        _emailService = emailService;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred");

            // Fire and forget email notification - don't delay user response
            _ = Task.Run(() => _emailService.SendCriticalErrorAsync(
                title: ex.GetType().Name,
                details: $"Message: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}\n\nRequest Path: {context.Request.Path}\nMethod: {context.Request.Method}"
            ));

            await HandleExceptionAsync(context);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var response = new
        {
            StatusCode = context.Response.StatusCode,
            Message = "An unexpected error occurred. The admin has been notified."
        };

        var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(jsonResponse);
    }
}
