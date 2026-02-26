using System.Text.Json;

namespace WebApi.Middleware;

/// <summary>
/// Middleware that validates an API key on HTTP requests to /api/ routes.
/// Controlled by the AUTH_HTTP_MODE environment variable:
///   - "none" (default): all requests pass through without auth
///   - "api-key-env": validates the X-API-Key header against HTTP_API_KEY env var
/// </summary>
public sealed class ApiKeyAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly bool _enabled;
    private readonly string? _expectedKey;

    public ApiKeyAuthMiddleware(RequestDelegate next)
    {
        _next = next;

        var mode = Environment.GetEnvironmentVariable("AUTH_HTTP_MODE") ?? "none";
        _enabled = string.Equals(mode, "api-key-env", StringComparison.OrdinalIgnoreCase);

        if (_enabled)
        {
            _expectedKey = Environment.GetEnvironmentVariable("HTTP_API_KEY");
            if (string.IsNullOrEmpty(_expectedKey))
            {
                Console.WriteLine(
                    "[Auth] WARNING: AUTH_HTTP_MODE=api-key-env but HTTP_API_KEY is not set. " +
                    "All /api/ requests will be rejected with 401.");
            }
        }
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_enabled)
        {
            await _next(context);
            return;
        }

        // Only protect /api/ routes
        var path = context.Request.Path.Value ?? string.Empty;
        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var providedKey = context.Request.Headers["X-API-Key"].FirstOrDefault();

        if (string.IsNullOrEmpty(providedKey) ||
            string.IsNullOrEmpty(_expectedKey) ||
            !string.Equals(providedKey, _expectedKey, StringComparison.Ordinal))
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = "Unauthorized" }));
            return;
        }

        await _next(context);
    }
}

public static class ApiKeyAuthMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyAuth(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ApiKeyAuthMiddleware>();
    }
}
