using Microsoft.AspNetCore.Antiforgery;

namespace TaskFlow.Api.Security;

/// <summary>
/// CSRF protection for cookie-authenticated SPA requests (double-submit token).
/// The API issues a JS-readable XSRF-TOKEN cookie; the SPA echoes it in the
/// X-XSRF-TOKEN header (Axios does this automatically). Every state-changing
/// request is validated against the HttpOnly antiforgery cookie.
/// </summary>
public static class CsrfProtection
{
    public const string HeaderName = "X-XSRF-TOKEN";
    public const string RequestTokenCookie = "XSRF-TOKEN";

    public static void IssueToken(HttpContext context)
    {
        var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
        var tokens = antiforgery.GetAndStoreTokens(context);
        context.Response.Cookies.Append(RequestTokenCookie, tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false, // must be readable by the SPA
            SameSite = SameSiteMode.Strict,
            Secure = context.Request.IsHttps,
            Path = "/",
        });
    }
}

public sealed class CsrfValidationMiddleware(RequestDelegate next, IAntiforgery antiforgery, ILogger<CsrfValidationMiddleware> logger)
{
    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase) { "GET", "HEAD", "OPTIONS", "TRACE" };

    public async Task InvokeAsync(HttpContext context)
    {
        if (!SafeMethods.Contains(context.Request.Method) && context.Request.Path.StartsWithSegments("/api"))
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException ex)
            {
                logger.LogWarning("CSRF validation failed for {Method} {Path}: {Reason}", context.Request.Method, context.Request.Path, ex.Message);
                await Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Invalid CSRF token",
                        detail: "The request is missing a valid anti-forgery token. Refresh the page and try again.",
                        extensions: new Dictionary<string, object?> { ["code"] = "csrf.invalid" })
                    .ExecuteAsync(context);
                return;
            }
        }

        await next(context);
    }
}
