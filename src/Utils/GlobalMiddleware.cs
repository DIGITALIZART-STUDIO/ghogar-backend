namespace GestionHogar.Utils;

public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger
    )
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
        catch (Exception ex)
        {
            // Log the exception - at least you're doing this right
            _logger.LogError(ex, "Unhandled exception occurred");

            // Clear any previous response
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            // Your error response object
            var response = new
            {
                Status = 500,
                Message = "Internal Server Error",
                // Only show detailed error in development
                Detail = context.RequestServices.GetService<IWebHostEnvironment>()?.IsDevelopment()
                == true
                    ? ex.ToString()
                    : null,
            };

            await context.Response.WriteAsJsonAsync(response);
        }
    }
}

// Extension method to make it look "cleaner" (as if that's possible in C#)
public static class GlobalExceptionHandlerMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app) =>
        app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
}
